namespace Claims.Application;

public class BusinessException(string field, string message, BusinessError error = BusinessError.Validation) : Exception(message)
{
    public string Field { get; } = field;
    public BusinessError Error { get; } = error;
}