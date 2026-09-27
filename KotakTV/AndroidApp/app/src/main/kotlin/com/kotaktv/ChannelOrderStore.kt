package com.kotaktv

import android.content.Context
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import com.kotaktv.data.Channel

class ChannelOrderStore(context: Context) {

    private val prefs = context.getSharedPreferences("channel_order", Context.MODE_PRIVATE)
    private val gson  = Gson()
    private val KEY   = "ordered_ids"

    fun save(channelIds: List<String>) {
        prefs.edit().putString(KEY, gson.toJson(channelIds)).apply()
    }

    fun load(): List<String> {
        val json = prefs.getString(KEY, null) ?: return emptyList()
        return runCatching {
            gson.fromJson<List<String>>(json, object : TypeToken<List<String>>() {}.type)
        }.getOrDefault(emptyList())
    }

    fun applyOrder(channels: List<Channel>): List<Channel> {
        val order = load()
        if (order.isEmpty()) return channels
        val indexMap = order.mapIndexed { i, id -> id to i }.toMap()
        return channels.sortedBy { indexMap[it.id] ?: Int.MAX_VALUE }
    }
}
