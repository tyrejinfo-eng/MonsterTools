# Build stage
FROM node:22-alpine AS builder

WORKDIR /app

# Install pnpm
RUN npm install -g pnpm

# Copy lockfile and package.json
COPY package.json pnpm-lock.yaml ./

# Install dependencies
RUN pnpm install --frozen-lockfile

# Copy all source files
COPY . .

# Build the app to create the dist folder
RUN pnpm run build

# Prepare the releasing package same as doing `npm publish`
RUN npm pack && \
  tar -zxvf *.tgz && \
  mv /app/package /app/prod

# Production stage
FROM gcr.io/distroless/nodejs22-debian12

WORKDIR /app

# Copy only the production files
COPY --from=builder /app/prod/ ./

CMD ["bin/cli.js"]
