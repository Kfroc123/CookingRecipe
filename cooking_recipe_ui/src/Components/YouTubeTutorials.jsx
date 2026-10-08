import { useEffect, useMemo, useState } from "react";
import { searchYouTubeVideos } from "../Utils/Recipes";

const DEFAULT_QUERY = "Nigerian food recipe";
const SEARCH_DEBOUNCE_MS = 350;

const getFriendlyVideoError = (message) => {
  if (!message) {
    return "Food tutorials are unavailable right now.";
  }

  if (message.toLowerCase().includes("api key")) {
    return "YouTube tutorials need a YouTube API key on the backend.";
  }

  return message;
};

function YouTubeTutorials({
  query,
  max = 4,
  heading = "Food tutorials",
  eyebrow = "Watch and learn",
  description = "",
  emptyMessage = "No tutorial videos found yet.",
}) {
  const [videos, setVideos] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const searchQuery = useMemo(() => {
    const trimmed = query?.trim();
    return trimmed || DEFAULT_QUERY;
  }, [query]);

  useEffect(() => {
    if (!searchQuery) return undefined;

    const controller = new AbortController();
    const timeoutId = setTimeout(async () => {
      setLoading(true);
      setError("");

      try {
        const data = await searchYouTubeVideos(searchQuery, max, {
          signal: controller.signal,
        });

        if (!controller.signal.aborted) {
          setVideos(Array.isArray(data) ? data : []);
        }
      } catch (err) {
        if (err.name !== "AbortError") {
          setVideos([]);
          setError(getFriendlyVideoError(err.message));
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false);
        }
      }
    }, SEARCH_DEBOUNCE_MS);

    return () => {
      controller.abort();
      clearTimeout(timeoutId);
    };
  }, [searchQuery, max]);

  return (
    <section className="video-section tutorial-section">
      <div className="video-section-header">
        <div>
          <p className="eyebrow video-eyebrow">{eyebrow}</p>
          <h3>{heading}</h3>
          {description && <p className="video-description">{description}</p>}
        </div>
      </div>

      {loading ? (
        <p className="video-status">Loading food tutorials...</p>
      ) : error ? (
        <p className="video-status">{error}</p>
      ) : videos.length > 0 ? (
        <div className="tutorial-grid">
          {videos.map((video) => (
            <article className="tutorial-card" key={video.videoId}>
              <div className="tutorial-frame">
                <iframe
                  src={`https://www.youtube.com/embed/${encodeURIComponent(video.videoId)}`}
                  title={video.title || "Food tutorial video"}
                  loading="lazy"
                  allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
                  allowFullScreen
                />
              </div>

              <div className="tutorial-content">
                <h4>{video.title}</h4>
                {video.channelTitle && <p>{video.channelTitle}</p>}
                <a href={video.watchUrl} target="_blank" rel="noreferrer">
                  Watch on YouTube
                </a>
              </div>
            </article>
          ))}
        </div>
      ) : (
        <p className="video-status">{emptyMessage}</p>
      )}
    </section>
  );
}

export default YouTubeTutorials;
