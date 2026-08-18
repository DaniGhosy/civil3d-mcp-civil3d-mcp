import fs from "node:fs";
import { McpServer, ResourceTemplate } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { CallToolResult } from "@modelcontextprotocol/sdk/types.js";
import { z } from "zod";
import { Civil3DHelpManager, selectRenderableImages } from "../help/helpIndex.js";
import type { HelpIndexStatus, HelpTopic, HelpVideoRef } from "../help/types.js";
import { getAutodeskVideoCatalog, renderVideoPlayerHtml } from "../help/videoCatalog.js";
import { captureToolHandler } from "./toolHandlerRegistry.js";

type ContentBlock = CallToolResult["content"][number];

/**
 * civil3d_help never talks to the C# AutoCAD plugin — it's 100% local (filesystem search over
 * Autodesk's own "Offline Help for Civil 3D <year>" package, if installed under Program Files,
 * plus a static bundled video catalog). Registered directly with server.tool()/server.resource()
 * instead of going through the DomainToolDefinition pattern, same as in the source project.
 *
 * Adapted from source for this repo's older MCP SDK (1.7.0 vs 1.29.0):
 *   - server.registerTool/registerResource (with description/annotations/outputSchema objects)
 *     don't exist here — uses the plain tool()/resource() overloads instead.
 *   - RequestHandlerExtra in this SDK only carries {signal, sessionId} — no sendNotification/
 *     progressToken, so the progress-streaming during first-time indexing was dropped. The final
 *     result is unaffected, callers just don't see intermediate "indexed N of M" notifications.
 *   - The "resource_link" content type doesn't exist in this SDK's CallToolResultSchema (only
 *     text/image/resource) — image and video "links" are embedded as an image content block or an
 *     HTML "resource" content block respectively; the JSON result still carries the raw uri/mp4Url
 *     fields as plain strings either way.
 */

interface HelpToolInput {
  action: "search" | "search_videos" | "get_topic" | "status" | "reindex";
  query?: string;
  id?: string;
  uri?: string;
  version?: string;
  featureArea?: string;
  topicType?: string;
  tags?: string[];
  limit?: number;
  includeImages?: boolean;
  maxImages?: number;
  includeVideos?: boolean;
  maxVideos?: number;
}

let defaultManager: Civil3DHelpManager | undefined;

export function getCivil3DHelpManager(): Civil3DHelpManager {
  defaultManager ??= new Civil3DHelpManager();
  return defaultManager;
}

export function registerHelpTool(
  server: McpServer,
  manager = getCivil3DHelpManager(),
  videoCatalog = getAutodeskVideoCatalog()
): void {
  const handler = async (rawInput: Record<string, unknown>): Promise<CallToolResult> => {
    const input = rawInput as unknown as HelpToolInput;
    try {
      if (input.action === "search_videos") {
        if (!input.query?.trim()) throw invalidInput("The search_videos action requires a non-empty query.");
        const videos = videoCatalog.search(input.query, input.maxVideos ?? 3);
        const result = { query: input.query, videos: videos.map(publicVideo) };
        return jsonResult(result, videoContentBlocks(videos));
      }

      if (input.action === "search") {
        if (!input.query?.trim()) throw invalidInput("The search action requires a non-empty query.");
        const result = await manager.search({
          query: input.query,
          version: input.version,
          featureArea: input.featureArea,
          topicType: input.topicType,
          tags: input.tags,
          limit: input.limit,
        });
        const videos = input.includeVideos === false ? [] : videoCatalog.search(input.query, input.maxVideos ?? 1);
        const response = { ...result, videos: videos.map(publicVideo) };
        return jsonResult(response, videoContentBlocks(videos));
      }

      if (input.action === "get_topic") {
        const idOrUri = input.uri ?? input.id;
        if (!idOrUri) throw invalidInput("The get_topic action requires id or uri.");
        const version = input.version ?? versionFromTopicUri(input.uri);
        const topic = await manager.getTopic(version, idOrUri);
        if (!topic) return toolError("Civil 3D help topic was not found.");
        const videos = input.includeVideos === false ? [] : videoCatalog.search(topic.title, input.maxVideos ?? 1);
        const result = { ...topicResult(topic), videos: videos.map(publicVideo) };
        const selectedImages = selectRenderableImages(topic, input.maxImages ?? 3);
        const content: ContentBlock[] = [{ type: "text", text: JSON.stringify(result, null, 2) }];
        if (input.includeImages !== false) {
          let totalBytes = 0;
          const maxTotalBytes = Number(process.env.CIVIL3D_HELP_MAX_IMAGE_BYTES ?? 6 * 1024 * 1024);
          for (const image of selectedImages) {
            if (totalBytes + image.size > maxTotalBytes) continue;
            content.push({
              type: "image",
              data: fs.readFileSync(image.path).toString("base64"),
              mimeType: image.mimeType,
            });
            totalBytes += image.size;
          }
        }
        content.push(...videoContentBlocks(videos));
        return { content };
      }

      if (input.action === "status") {
        const result = {
          ...(await manager.status(input.version)),
          videoCatalog: videoCatalog.status(),
        };
        return jsonResult(result);
      }

      if (process.env.CIVIL3D_ENABLE_HELP_REINDEX !== "true") {
        return toolError("Civil 3D help reindex is disabled. Set CIVIL3D_ENABLE_HELP_REINDEX=true to enable it.");
      }
      const status = await manager.reindex(input.version);
      return jsonResult(safeIndexStatus(status));
    } catch (error) {
      return toolError(error instanceof Error ? error.message : String(error));
    }
  };

  captureToolHandler("civil3d_help", handler);
  server.tool(
    "civil3d_help",
    "Search installed Autodesk Civil 3D offline help, retrieve cited topics with images and " +
      "playable Autodesk videos, inspect index status, or rebuild the local index. Requires " +
      "Autodesk's separate 'Offline Help for Civil 3D <year>' package to be installed under " +
      "Program Files (or CIVIL3D_HELP_ROOT set) for search/get_topic/status/reindex — without it " +
      "those actions return a clear error. search_videos always works: it queries a small static " +
      "bundled catalog (autodesk_videos.json, Civil 3D 2026/English, help.autodesk.com URLs only) " +
      "with no filesystem dependency.",
    {
      action: z.enum(["search", "search_videos", "get_topic", "status", "reindex"]),
      query: z.string().optional(),
      id: z.string().optional(),
      uri: z.string().optional(),
      version: z.string().regex(/^\d{4}$/).optional(),
      featureArea: z.string().optional(),
      topicType: z.string().optional(),
      tags: z.array(z.string()).optional(),
      limit: z.number().int().min(1).max(20).optional(),
      includeImages: z.boolean().optional(),
      maxImages: z.number().int().min(0).max(5).optional(),
      includeVideos: z.boolean().optional(),
      maxVideos: z.number().int().min(0).max(5).optional(),
    },
    handler
  );
}

export function registerHelpResources(
  server: McpServer,
  manager = getCivil3DHelpManager(),
  videoCatalog = getAutodeskVideoCatalog()
): void {
  server.resource(
    "civil3d-help-topic",
    new ResourceTemplate("civil3d://help/topics/{version}/{topicId}", { list: undefined }),
    { description: "Cleaned, version-matched topic from locally installed Autodesk Civil 3D offline help.", mimeType: "text/markdown" },
    async (uri, variables) => {
      const version = String(variables.version ?? "");
      const topicId = String(variables.topicId ?? "");
      const topic = await manager.getTopic(version, topicId);
      if (!topic) throw new Error("Civil 3D help topic was not found.");
      return { contents: [{ uri: uri.href, mimeType: "text/markdown", text: topic.markdown }] };
    }
  );

  server.resource(
    "civil3d-help-image",
    new ResourceTemplate("civil3d://help/images/{version}/{imageId}", { list: undefined }),
    { description: "Screenshot or diagram referenced by a locally installed Autodesk Civil 3D help topic.", mimeType: "image/png" },
    async (uri, variables) => {
      const version = String(variables.version ?? "");
      const imageId = String(variables.imageId ?? "");
      const image = await manager.getImage(version, imageId);
      if (!image) throw new Error("Civil 3D help image was not found.");
      return {
        contents: [{ uri: uri.href, mimeType: image.mimeType, blob: fs.readFileSync(image.path).toString("base64") }],
      };
    }
  );

  server.resource(
    "civil3d-help-video",
    new ResourceTemplate("civil3d://help/videos/{version}/{videoId}", { list: undefined }),
    { description: "Playable Autodesk Civil 3D help video with a direct MP4 fallback.", mimeType: "text/html" },
    async (uri, variables) => {
      const version = String(variables.version ?? "");
      const videoId = String(variables.videoId ?? "");
      const video = videoCatalog.get(videoId);
      if (!video || video.sourceVersion !== version) throw new Error("Civil 3D help video was not found.");
      return { contents: [{ uri: uri.href, mimeType: "text/html", text: renderVideoPlayerHtml(video) }] };
    }
  );
}

function topicResult(topic: HelpTopic) {
  const debugPaths = process.env.CIVIL3D_DEBUG_PATHS === "true";
  return {
    markdown: topic.markdown,
    metadata: {
      id: topic.id,
      topicId: topic.topicId,
      uri: topic.uri,
      product: topic.product,
      version: topic.version,
      featureArea: topic.featureArea,
      topicType: topic.topicType,
      title: topic.title,
      headings: topic.headings,
      summary: topic.summary,
      tags: topic.tags,
      canonicalUrl: topic.canonicalUrl,
      contentHash: topic.contentHash,
      ...(debugPaths ? { sourcePath: topic.sourcePath } : {}),
    },
    relatedTopicIds: topic.relatedTopicIds,
    images: topic.images.map(({ path: _path, ...image }) => image),
  };
}

function jsonResult(result: unknown, additionalContent: ContentBlock[] = []): CallToolResult {
  return {
    content: [{ type: "text", text: JSON.stringify(result, null, 2) }, ...additionalContent],
  };
}

function publicVideo(video: HelpVideoRef) {
  return {
    id: video.id,
    uri: video.uri,
    title: video.title,
    sourceVersion: video.sourceVersion,
    pageUrl: video.pageUrl,
    mp4Url: video.mp4Url,
    webmUrl: video.webmUrl,
  };
}

function videoContentBlocks(videos: HelpVideoRef[]): ContentBlock[] {
  return videos.map((video) => ({
    type: "resource" as const,
    resource: {
      uri: video.uri,
      mimeType: "text/html",
      text: renderVideoPlayerHtml(video),
    },
  }));
}

function toolError(message: string): CallToolResult {
  return { content: [{ type: "text", text: message }], isError: true };
}

function invalidInput(message: string): Error {
  return new Error(message);
}

function versionFromTopicUri(uri?: string): string | undefined {
  if (!uri) return undefined;
  try {
    const segments = new URL(uri).pathname.split("/").filter(Boolean);
    return segments[0] === "topics" ? segments[1] : undefined;
  } catch {
    return undefined;
  }
}

function safeIndexStatus(status: HelpIndexStatus) {
  const { root, cachePath, ...safe } = status;
  return process.env.CIVIL3D_DEBUG_PATHS === "true" ? { ...safe, root, cachePath } : safe;
}
