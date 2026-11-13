namespace FrontendNet.Models;

/// <summary>API error body: { status?, message, errors?: [{ type, msg, path, location, value }] }.</summary>
public class ApiError
{
    public int? Status { get; set; }
    public string? Message { get; set; }
    public List<ApiFieldError>? Errors { get; set; }
}

public class ApiFieldError
{
    public string? Msg { get; set; }
    public string? Path { get; set; }
}
