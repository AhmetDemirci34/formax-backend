package com.kotaktv

import android.content.Intent
import android.os.Bundle
import android.view.KeyEvent
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.ImageView
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import androidx.recyclerview.widget.GridLayoutManager
import androidx.recyclerview.widget.RecyclerView
import com.bumptech.glide.Glide
import com.kotaktv.data.Channel
import com.kotaktv.data.ChannelRepository
import com.kotaktvapp.BuildConfig
import com.kotaktvapp.R
import kotlinx.coroutines.launch

class ChannelListActivity : AppCompatActivity() {

    private lateinit var channelGrid: RecyclerView
    private lateinit var loadingView: View
    private lateinit var errorView: TextView
    private val repository = ChannelRepository()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_channel_list)
        channelGrid = findViewById(R.id.channel_grid)
        loadingView = findViewById(R.id.loading_view)
        errorView   = findViewById(R.id.error_view)
        channelGrid.layoutManager = GridLayoutManager(this, 6)
        loadChannels()
    }

    private fun loadChannels() {
        loadingView.visibility = View.VISIBLE
        channelGrid.visibility = View.GONE
        errorView.visibility   = View.GONE

        lifecycleScope.launch {
            repository.getChannels(BuildConfig.KANALLAR_JSON_URL).fold(
                onSuccess = { channels ->
                    channelGrid.adapter = ChannelAdapter(channels) { channel ->
                        startActivity(
                            Intent(this@ChannelListActivity, PlayerActivity::class.java)
                                .putExtra(PlayerActivity.EXTRA_CHANNEL_ID, channel.id)
                        )
                    }
                    loadingView.visibility = View.GONE
                    channelGrid.visibility = View.VISIBLE
                    if (channels.isNotEmpty()) channelGrid.post {
                        channelGrid.getChildAt(0)?.requestFocus()
                    }
                },
                onFailure = { err ->
                    loadingView.visibility = View.GONE
                    errorView.text         = "Kanal listesi yüklenemedi:\n${err.message}"
                    errorView.visibility   = View.VISIBLE
                }
            )
        }
    }
}

private class ChannelAdapter(
    private val channels: List<Channel>,
    private val onClick: (Channel) -> Unit
) : RecyclerView.Adapter<ChannelAdapter.VH>() {

    inner class VH(val root: View) : RecyclerView.ViewHolder(root) {
        val logo: ImageView = root.findViewById(R.id.channel_logo)
        val name: TextView  = root.findViewById(R.id.channel_name)
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): VH {
        val view = LayoutInflater.from(parent.context)
            .inflate(R.layout.item_channel, parent, false)
        return VH(view)
    }

    override fun onBindViewHolder(holder: VH, position: Int) {
        val ch = channels[position]
        holder.name.text = ch.name
        Glide.with(holder.root)
            .load(ch.logoUrl)
            .placeholder(android.R.drawable.ic_menu_slideshow)
            .error(android.R.drawable.ic_menu_slideshow)
            .into(holder.logo)

        holder.root.setOnClickListener { onClick(ch) }

        holder.root.setOnFocusChangeListener { v, hasFocus ->
            v.animate()
                .scaleX(if (hasFocus) 1.12f else 1f)
                .scaleY(if (hasFocus) 1.12f else 1f)
                .setDuration(150)
                .start()
        }

        holder.root.setOnKeyListener { _, keyCode, event ->
            if (event.action == KeyEvent.ACTION_DOWN &&
                (keyCode == KeyEvent.KEYCODE_DPAD_CENTER || keyCode == KeyEvent.KEYCODE_ENTER)
            ) {
                onClick(ch)
                true
            } else false
        }
    }

    override fun getItemCount() = channels.size
}
