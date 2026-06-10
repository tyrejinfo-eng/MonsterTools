using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MonsterTools.Services
{
    public interface ILMStudioService
    {
        Task<string> QueryModelAsync(string prompt, double temperature = 0.2);
        
        /// <summary>
        /// Executes a multi-turn chat history evaluation with the local model runtime.
        /// </summary>
        Task<string> QueryModelHistoryAsync(List<ChatMessageContext> conversationHistory, CancellationToken cancellationToken);
    }

    public class ChatMessageContext
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
