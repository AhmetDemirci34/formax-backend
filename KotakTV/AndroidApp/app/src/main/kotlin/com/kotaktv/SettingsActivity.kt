package com.kotaktv

import android.os.Bundle
import android.text.Editable
import android.text.TextWatcher
import android.view.KeyEvent
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.view.inputmethod.EditorInfo
import android.view.inputmethod.InputMethodManager
import android.widget.Button
import android.widget.EditText
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity
import androidx.lifecycle.lifecycleScope
import androidx.recyclerview.widget.LinearLayoutManager
import androidx.recyclerview.widget.RecyclerView
import com.kotaktv.data.Channel
import com.kotaktv.data.ChannelRepository
import com.kotaktvapp.BuildConfig
import com.kotaktvapp.R
import kotlinx.coroutines.launch

class SettingsActivity : AppCompatActivity() {

    private lateinit var rvChannels: RecyclerView
    private lateinit var etSearch: EditText
    private val repository = ChannelRepository()
    private lateinit var store: ChannelOrderStore
    private lateinit var adapter: ChannelOrderAdapter

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_settings)

        store      = ChannelOrderStore(this)
        rvChannels = findViewById(R.id.rv_channels)
        etSearch   = findViewById(R.id.et_search)

        rvChannels.layoutManager = LinearLayoutManager(this)
        findViewById<Button>(R.id.btn_close_settings).setOnClickListener { finish() }

        etSearch.addTextChangedListener(object : TextWatcher {
            override fun afterTextChanged(s: Editable?) {
                if (::adapter.isInitialized) adapter.filter(s?.toString() ?: "")
            }
            override fun beforeTextChanged(s: CharSequence?, start: Int, count: Int, after: Int) {}
            override fun onTextChanged(s: CharSequence?, start: Int, before: Int, count: Int) {}
        })

        loadChannels()
    }

    private fun loadChannels() {
        lifecycleScope.launch {
            repository.getChannels(BuildConfig.KANALLAR_JSON_URL).fold(
                onSuccess = { channels ->
                    val ordered = store.applyOrder(channels)
                    adapter = ChannelOrderAdapter(ordered.toMutableList(), store)
                    rvChannels.adapter = adapter
                },
                onFailure = {}
            )
        }
    }
}

// ─── Adapter ─────────────────────────────────────────────────────────────────

class ChannelOrderAdapter(
    private val allChannels: MutableList<Channel>,
    private val store: ChannelOrderStore
) : RecyclerView.Adapter<ChannelOrderAdapter.VH>() {

    private var displayList: List<Channel> = allChannels.toList()
    private var currentFilter = ""

    inner class VH(val root: View) : RecyclerView.ViewHolder(root) {
        val etPosition: EditText = root.findViewById(R.id.et_position)
        val tvName: TextView     = root.findViewById(R.id.tv_channel_name)
        val btnUp: Button        = root.findViewById(R.id.btn_move_up)
        val btnDown: Button      = root.findViewById(R.id.btn_move_down)
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): VH {
        val view = LayoutInflater.from(parent.context)
            .inflate(R.layout.item_channel_order, parent, false)
        return VH(view)
    }

    override fun onBindViewHolder(holder: VH, position: Int) {
        val channel     = displayList[position]
        val fullPos     = allChannels.indexOf(channel) + 1
        val isFiltering = currentFilter.isNotEmpty()

        // Numara kutucuğu: mevcut pozisyonu göster
        holder.etPosition.setText("$fullPos")
        holder.tvName.text = ChannelNameCleaner.clean(channel.name)

        holder.btnUp.visibility   = if (isFiltering) View.INVISIBLE else View.VISIBLE
        holder.btnDown.visibility = if (isFiltering) View.INVISIBLE else View.VISIBLE

        // ▲▼ butonları
        holder.btnUp.setOnClickListener   { moveUp(channel) }
        holder.btnDown.setOnClickListener { moveDown(channel) }

        // Direkt numara girişi — Done veya Enter'a basıldığında
        holder.etPosition.setOnEditorActionListener { v, actionId, event ->
            val isDone = actionId == EditorInfo.IME_ACTION_DONE
            val isEnter = event?.keyCode == KeyEvent.KEYCODE_ENTER
                    && event.action == KeyEvent.ACTION_DOWN
            if (isDone || isEnter) {
                applyPositionInput(holder, channel)
                true
            } else false
        }

        // Focus ayrıldığında da uygula
        holder.etPosition.setOnFocusChangeListener { v, hasFocus ->
            if (!hasFocus) applyPositionInput(holder, channel)
        }
    }

    override fun getItemCount() = displayList.size

    fun filter(query: String) {
        currentFilter = query.trim()
        displayList = if (currentFilter.isEmpty()) allChannels.toList()
        else allChannels.filter {
            ChannelNameCleaner.clean(it.name).contains(currentFilter, ignoreCase = true)
        }
        notifyDataSetChanged()
    }

    // ─── Pozisyon doğrudan girişi ─────────────────────────────────────────────

    private fun applyPositionInput(holder: VH, channel: Channel) {
        val typed = holder.etPosition.text.toString().toIntOrNull() ?: return
        val target = (typed - 1).coerceIn(0, allChannels.size - 1)
        val current = allChannels.indexOf(channel)
        if (current == target) return

        allChannels.removeAt(current)
        allChannels.add(target, channel)
        persistAndRefresh()

        // Klavyeyi kapat
        val imm = holder.root.context
            .getSystemService(android.content.Context.INPUT_METHOD_SERVICE) as InputMethodManager
        imm.hideSoftInputFromWindow(holder.etPosition.windowToken, 0)
        holder.etPosition.clearFocus()
    }

    // ─── Taşıma ───────────────────────────────────────────────────────────────

    private fun moveUp(channel: Channel) {
        val idx = allChannels.indexOf(channel)
        if (idx <= 0) return
        allChannels.removeAt(idx)
        allChannels.add(idx - 1, channel)
        persistAndRefresh()
    }

    private fun moveDown(channel: Channel) {
        val idx = allChannels.indexOf(channel)
        if (idx >= allChannels.size - 1) return
        allChannels.removeAt(idx)
        allChannels.add(idx + 1, channel)
        persistAndRefresh()
    }

    private fun persistAndRefresh() {
        store.save(allChannels.map { it.id })
        filter(currentFilter)
    }
}
