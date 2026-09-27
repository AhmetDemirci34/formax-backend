package com.kotaktv

object ChannelNameCleaner {
    private val TECH_TAGS = Regex(
        """[\(\[]\s*(?:1440p?|4K|2160p?|1080p?|720p?|480p?|360p?|240p?|SD|HD|FHD|UHD|HDTV|Not\s*24/?7|24/?7|HLS|HEVC)\s*[\)\]]|""" +
        """\s+(?:1440p?|4K|2160p?|1080p?|720p?|480p?|360p?|240p?|HDTV|Not\s*24/?7|24/?7)\s*$""",
        setOf(RegexOption.IGNORE_CASE)
    )

    fun clean(name: String): String = TECH_TAGS.replace(name, "").trim()
}
