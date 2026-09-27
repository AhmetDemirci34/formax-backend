package com.kotaktv

import android.content.Intent
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.view.View
import android.view.WindowManager
import android.widget.ProgressBar
import androidx.appcompat.app.AppCompatActivity
import com.kotaktvapp.R

class SplashActivity : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        setContentView(R.layout.activity_splash)

        val logoBlock  = findViewById<View>(R.id.splash_logo_block)
        val progressBar = findViewById<ProgressBar>(R.id.splash_progress)

        // Logo: sıfırdan büyüyüp beliriyor
        logoBlock.scaleX = 0.6f
        logoBlock.scaleY = 0.6f
        logoBlock.animate()
            .alpha(1f)
            .scaleX(1f)
            .scaleY(1f)
            .setDuration(700)
            .setStartDelay(150)
            .withEndAction {
                // İnce progress çubuğu beliriyor
                progressBar.animate()
                    .alpha(1f)
                    .setDuration(400)
                    .withEndAction {
                        // 1.8s sonra ana ekrana geç
                        Handler(Looper.getMainLooper()).postDelayed({
                            startActivity(Intent(this, MainActivity::class.java))
                            finish()
                            overridePendingTransition(android.R.anim.fade_in, android.R.anim.fade_out)
                        }, 1800)
                    }
                    .start()
            }
            .start()
    }
}
