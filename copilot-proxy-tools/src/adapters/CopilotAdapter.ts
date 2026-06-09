export class CopilotAdapter {
    static toMonsterTools(body: CopilotRequest): MonsterToolsRequest {
        const prompt =
            body.messages
                ?.map(x => x.content)
                .join("\n") ?? "";

        return {
            prompt,
            stream: body.stream ?? true
        };
    }
}