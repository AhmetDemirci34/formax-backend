package com.kotaktv.data

import com.google.gson.annotations.SerializedName

// ─── Kök JSON nesnesi ────────────────────────────────────────────────────────

data class KanallarResponse(
    @SerializedName("_meta")    val meta: Meta,
    @SerializedName("channels") val channels: List<Channel>
)

data class Meta(
    @SerializedName("schema_version")  val schemaVersion: String,
    @SerializedName("generated_at")    val generatedAt: String,
    @SerializedName("ttl_seconds")     val ttlSeconds: Int,
    @SerializedName("total_channels")  val totalChannels: Int
)

// ─── Kanal ───────────────────────────────────────────────────────────────────

data class Channel(
    @SerializedName("id")        val id: String,
    @SerializedName("name")      val name: String,
    @SerializedName("logo_url")  val logoUrl: String,
    @SerializedName("category")  val category: String,
    @SerializedName("tags")      val tags: List<String> = emptyList(),
    @SerializedName("epg_id")    val epgId: String = "",
    @SerializedName("is_active") val isActive: Boolean = true,
    @SerializedName("streams")   val streams: List<Stream>
) {
    /** priority sırasına göre sıralanmış stream listesi — FailoverEngine kullanır. */
    val sortedStreams: List<Stream> get() = streams.sortedBy { it.priority }
}

// ─── Stream ──────────────────────────────────────────────────────────────────

data class Stream(
    @SerializedName("priority")    val priority: Int,
    @SerializedName("label")       val label: String,
    @SerializedName("url")         val url: String,
    @SerializedName("drm")         val drm: DrmConfig? = null,
    @SerializedName("resolution")  val resolution: String = "auto",
    @SerializedName("bitrate_kbps") val bitrateKbps: Int? = null,
    @SerializedName("headers")     val headers: Map<String, String> = emptyMap()
)

// ─── DRM ─────────────────────────────────────────────────────────────────────

data class DrmConfig(
    @SerializedName("type")        val type: String,           // "widevine" | "playready"
    @SerializedName("license_url") val licenseUrl: String,
    @SerializedName("headers")     val headers: Map<String, String> = emptyMap()
)
