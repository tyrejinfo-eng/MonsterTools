using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using MonsterTools.Core;

namespace MonsterTools.Workers
{
    public class FileSystemWorkers : IToolWorker
    {
        private readonly ILogger<FileSystemWorkers> _logger;
        public string Name => "FileSystemTool";
        public string Description => "Handles non-blocking local filesystem IO file write mutations for code injection.";

        public FileSystemWorkers(ILogger<FileSystemWorkers> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ToolResult> ExecuteAsync(ToolRequest request)
        {
            if (!request.Arguments.TryGetValue("filePath", out var rawPath) || 
                !request.Arguments.TryGetValue("content", out var rawContent))
            {
                return ToolResult.Failure("FileSystemTool requires 'filePath' and 'content' string parameters.");
            }

            string cleanPath = rawPath.ToString()?.Trim('\"', '\'') ?? string.Empty;
            string fileContent = rawContent.ToString() ?? string.Empty;

            try
            {
                string destinationFile = Path.GetFullPath(cleanPath);
                string? parentDirectory = Path.GetDirectoryName(destinationFile);

                if (!string.IsNullOrEmpty(parentDirectory) && !Directory.Exists(parentDirectory))
                {
                    Directory.CreateDirectory(parentDirectory);
                }

                await File.WriteAllTextAsync(destinationFile, fileContent, Encoding.UTF8);
                _logger.LogInformation("Successfully wrote file mutation block: {Path}", destinationFile);
                
                return ToolResult.Success($"File correctly generated at path target location: {destinationFile}");
            }
            catch (Exception ex)
            {
                return ToolResult.Failure($"Filesystem IO resource error encountered: {ex.Message}");
            }
        }
    }
}
