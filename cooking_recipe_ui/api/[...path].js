const normalizeBaseUrl = (value) => value?.trim().replace(/\/+$/, "") || "";

const getBackendApiBaseUrl = () => {
  const backendBaseUrl = normalizeBaseUrl(process.env.BACKEND_API_BASE_URL);

  if (!backendBaseUrl) {
    throw new Error("Missing BACKEND_API_BASE_URL.");
  }

  return backendBaseUrl.endsWith("/api") ? backendBaseUrl : `${backendBaseUrl}/api`;
};

const readBody = (request) =>
  new Promise((resolve, reject) => {
    const chunks = [];

    request.on("data", (chunk) => chunks.push(chunk));
    request.on("end", () => resolve(Buffer.concat(chunks)));
    request.on("error", reject);
  });

const getPathSegments = (request) => {
  const path = request.query?.path;

  if (Array.isArray(path)) {
    return path;
  }

  return path ? [path] : [];
};

const copyRequestHeaders = (headers) => {
  const blockedHeaders = new Set([
    "connection",
    "content-length",
    "host",
    "x-forwarded-host",
    "x-forwarded-proto",
  ]);

  const nextHeaders = {};

  for (const [key, value] of Object.entries(headers)) {
    if (!blockedHeaders.has(key.toLowerCase()) && value !== undefined) {
      nextHeaders[key] = Array.isArray(value) ? value.join(", ") : value;
    }
  }

  return nextHeaders;
};

const copyResponseHeaders = (source, target) => {
  const blockedHeaders = new Set([
    "connection",
    "content-encoding",
    "content-length",
    "keep-alive",
    "transfer-encoding",
  ]);

  source.headers.forEach((value, key) => {
    if (!blockedHeaders.has(key.toLowerCase())) {
      target.setHeader(key, value);
    }
  });
};

export default async function handler(request, response) {
  try {
    const backendApiBaseUrl = getBackendApiBaseUrl();
    const incomingUrl = new URL(request.url, `https://${request.headers.host || "localhost"}`);
    const path = getPathSegments(request).map(encodeURIComponent).join("/");
    const targetUrl = new URL(path ? `${backendApiBaseUrl}/${path}` : backendApiBaseUrl);
    targetUrl.search = incomingUrl.search;

    const hasBody = !["GET", "HEAD"].includes(request.method || "GET");
    const body = hasBody ? await readBody(request) : undefined;

    const backendResponse = await fetch(targetUrl, {
      method: request.method,
      headers: copyRequestHeaders(request.headers),
      body,
      redirect: "manual",
    });

    response.statusCode = backendResponse.status;
    copyResponseHeaders(backendResponse, response);

    const responseBody = Buffer.from(await backendResponse.arrayBuffer());
    response.end(responseBody);
  } catch (error) {
    response.statusCode = 500;
    response.setHeader("Content-Type", "application/json");
    response.end(
      JSON.stringify({
        error: error instanceof Error ? error.message : "API proxy failed.",
      })
    );
  }
}
