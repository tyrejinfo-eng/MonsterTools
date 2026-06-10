namespace MonsterTools.Core
{
    public class ToolResult
    {
        public bool IsSuccess { get; set; }
        public string Output { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;

        public static ToolResult Success(string output) => 
            new() { IsSuccess = true, Output = output };

        public static ToolResult Failure(string errorMessage) => 
            new() { IsSuccess = false, ErrorMessage = errorMessage };
    }
}
