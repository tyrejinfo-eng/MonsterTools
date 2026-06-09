export class OpenAIAdapter {
    static completion(content:string) {
        return {
            id: crypto.randomUUID(),
            object: "chat.completion",
            created: Date.now(),
            model: "granite",
            choices: [
                {
                    index: 0,
                    finish_reason: "stop",
                    message: {
                        role: "assistant",
                        content
                    }
                }
            ]
        };
    }
}