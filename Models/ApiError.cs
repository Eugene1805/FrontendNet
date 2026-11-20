namespace FrontendNet.Models;

/// <summary>Error payload returned by the backend API: { status, message, errors? }.</summary>
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
