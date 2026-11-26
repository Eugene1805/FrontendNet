#!/usr/bin/env bash
set -uo pipefail

# Verificación de despliegue VM 1 (frontendnet + Apache 2).
# Emite únicamente líneas PASS/FAIL sin imprimir secretos ni valores de .env.

if [[ "${1:-}" != "vm1" ]]; then
  echo "Uso: $0 vm1" >&2
  exit 2
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
ENV_FILE="$REPO_ROOT/.env"
CA_BUNDLE="$REPO_ROOT/.certs/ca-bundle.crt"
SYSTEM_CA="/usr/local/share/ca-certificates/team-ca.crt"

PASS_COUNT=0
FAIL_COUNT=0

record_pass() {
  PASS_COUNT=$((PASS_COUNT + 1))
  printf 'PASS | %s\n' "$1"
}

record_fail() {
  FAIL_COUNT=$((FAIL_COUNT + 1))
  printf 'FAIL | %s\n' "$1"
}

check_cmd() {
  local label="$1"
  shift
  if "$@" >/dev/null 2>&1; then
    record_pass "$label"
  else
    record_fail "$label"
  fi
}

read_env_value() {
  local key="$1"
  local file="$2"
  [[ -f "$file" ]] || return 1
  awk -F= -v k="$key" '$1 == k { sub(/^[^=]*=/, ""); gsub(/^[ \t"]+|[ \t"\r]+$/, ""); print; exit }' "$file"
}

check_perm() {
  local label="$1"
  local target="$2"
  local expected="$3"
  if [[ ! -e "$target" ]]; then
    record_fail "$label"
    return
  fi
  local mode
  mode="$(stat -c '%a' "$target" 2>/dev/null || echo "")"
  if [[ "$mode" == "$expected" ]]; then
    record_pass "$label"
  else
    record_fail "$label"
  fi
}

# 1. Hora sincronizada
if [[ "$(timedatectl show -p NTPSynchronized --value 2>/dev/null || echo no)" == "yes" ]]; then
  record_pass "timedatectl_ntp_synchronized"
else
  record_fail "timedatectl_ntp_synchronized"
fi

# 2. /etc/hosts con los tres nombres
if grep -qE '[[:space:]]tienda\.lan([[:space:]]|$)' /etc/hosts \
  && grep -qE '[[:space:]]api\.tienda\.lan([[:space:]]|$)' /etc/hosts \
  && grep -qE '[[:space:]]db\.tienda\.lan([[:space:]]|$)' /etc/hosts; then
  record_pass "etc_hosts_entries"
else
  record_fail "etc_hosts_entries"
fi

# 3. Permisos de .env y .certs/ca-bundle.crt
check_perm "perm_env_600" "$ENV_FILE" "600"
check_perm "perm_ca_bundle_644" "$CA_BUNDLE" "644"

# 4. IP_VM2 válida en .env (sin mostrar su valor en la salida)
IP_VM2="$(read_env_value "IP_VM2" "$ENV_FILE" || true)"
if [[ -n "$IP_VM2" && "$IP_VM2" != "127.0.0.1" && "$IP_VM2" != "0.0.0.0" ]]; then
  record_pass "env_ip_vm2_configured"
else
  record_fail "env_ip_vm2_configured"
fi

# 5. CA interna instalada en sistema y presente en ca-certificates.crt
if [[ -f "$SYSTEM_CA" ]] && CA_FP="$(openssl x509 -in "$SYSTEM_CA" -noout -fingerprint -sha256 2>/dev/null)" \
  && grep -Fq "${CA_FP#*=}" <(openssl crl2pkcs7 -nocrl -certfile /etc/ssl/certs/ca-certificates.crt 2>/dev/null | openssl pkcs7 -print_certs -noout 2>/dev/null || true); then
  record_pass "system_ca_installed"
elif [[ -f "$SYSTEM_CA" ]] && openssl verify -CAfile /etc/ssl/certs/ca-certificates.crt "$SYSTEM_CA" >/dev/null 2>&1; then
  record_pass "system_ca_installed"
else
  record_fail "system_ca_installed"
fi

# 6. Contenedor ecommerce_frontend activo y resolviendo api.tienda.lan hacia IP_VM2
if [[ "$(docker inspect -f '{{.State.Running}}' ecommerce_frontend 2>/dev/null || echo false)" == "true" ]]; then
  record_pass "container_frontend_running"
else
  record_fail "container_frontend_running"
fi

if [[ -n "$IP_VM2" ]] && RESOLVED_API="$(docker exec ecommerce_frontend getent hosts api.tienda.lan 2>/dev/null | awk '{print $1}' | head -n1)" && [[ "$RESOLVED_API" == "$IP_VM2" ]]; then
  record_pass "container_extra_hosts_api"
else
  record_fail "container_extra_hosts_api"
fi

# 7. Salud HTTP en loopback del contenedor (simula X-Forwarded-Proto de Apache para evitar redirección HTTPS en sonda local)
check_cmd "frontend_loopback_health" curl -fsS -o /dev/null --max-time 5 -H "X-Forwarded-Proto: https" http://127.0.0.1:5000/

# 8. Puertos en escucha y enlace estricto en 127.0.0.1:5000
SS_OUT="$(ss -tulpn 2>/dev/null || true)"
if echo "$SS_OUT" | grep -qE '127\.0\.0\.1:5000([[:space:]]|$)' \
  && ! echo "$SS_OUT" | grep -qE '(0\.0\.0\.0|\[::\]|\*):5000([[:space:]]|$)' \
  && echo "$SS_OUT" | grep -qE ':443([[:space:]]|$)' \
  && echo "$SS_OUT" | grep -qE ':80([[:space:]]|$)'; then
  record_pass "listening_ports_bound"
else
  record_fail "listening_ports_bound"
fi

# 9. Reglas UFW activas para Apache
UFW_OUT="$(ufw status 2>/dev/null || sudo -n ufw status 2>/dev/null || true)"
if echo "$UFW_OUT" | grep -q "Status: active" \
  && echo "$UFW_OUT" | grep -qE 'Apache Full|80/tcp|443/tcp'; then
  record_pass "ufw_active_apache_rules"
else
  record_fail "ufw_active_apache_rules"
fi

# 10. Handshake TLS local (tienda.lan:443) y hacia siguiente nodo (api.tienda.lan:443)
check_cmd "tls_local_tienda_lan" openssl s_client -connect tienda.lan:443 -servername tienda.lan -CAfile /etc/ssl/certs/ca-certificates.crt -verify_return_error </dev/null
check_cmd "tls_next_hop_api_tienda_lan" openssl s_client -connect api.tienda.lan:443 -servername api.tienda.lan -CAfile "$CA_BUNDLE" -verify_return_error </dev/null

printf 'SUMMARY | PASS=%d FAIL=%d\n' "$PASS_COUNT" "$FAIL_COUNT"
[[ "$FAIL_COUNT" -eq 0 ]]
