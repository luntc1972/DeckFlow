# Content KB Publish Directory

`content-kb/` is the tracked publish directory for the public content knowledge base. The site deploy reads these files from the runtime image after the Dockerfile copies this directory to `/app/content-kb`.

## Commit-Then-Deploy Flow

1. Export the local index seed:

   ```bash
   dotnet run --project DeckFlow.CLI/DeckFlow.CLI.csproj -- content-index-export --output content-kb/seed/index-seed.json
   ```

2. Copy generated artifacts from the local distill output into the tracked tree:

   ```bash
   cp -R artifacts/content-kb/* content-kb/
   ```

3. Review the seed and copied markdown artifacts, then commit the tracked `content-kb/` changes.

4. Deploy from git. The Docker runtime stage copies `content-kb/` into `/app/content-kb`, so resolver code should combine `ContentRootPath` with seed `artifactPath` values such as `content-kb/source/video.md`.

`artifacts/` stays ignored because it is local generation output. Only the curated publish tree under `content-kb/` is committed.
