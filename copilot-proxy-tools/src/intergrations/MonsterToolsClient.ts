export class MonsterToolsClient {
    constructor(
        private readonly baseUrl:string
    ) {}

    async executeAgent(
        prompt:string,
        workspace:string
    ) {
        const response =
            await fetch(
                `${this.baseUrl}/api/agent`,
                {
                    method:"POST",
                    headers:{
                        "Content-Type":"application/json"
                    },
                    body:JSON.stringify({
                        prompt,
                        workspace
                    })
                }
            );

        if(!response.ok)
            throw new Error(
                await response.text()
            );

        return response.json();
    }
}