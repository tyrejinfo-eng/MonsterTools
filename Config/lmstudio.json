export class LMStudioAdapter {
    constructor(
        private readonly baseUrl:string
    ) {}

    async health() {
        const r =
            await fetch(
                `${this.baseUrl}/v1/models`
            );

        return r.ok;
    }
}