namespace Pharmacy.Shared.Results;

public class ApiResponse<T>
{
    public T? Data { get; init; }
    public bool Success { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static ApiResponse<T> Ok(T data) => new()
    {
        Data = data,
        Success = true,
        Errors = []
    };

    public static ApiResponse<T> Fail(params string[] errors) => new()
    {
        Data = default,
        Success = false,
        Errors = errors
    };
}
