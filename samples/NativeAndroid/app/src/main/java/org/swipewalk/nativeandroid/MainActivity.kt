package org.swipewalk.nativeandroid

import android.content.Intent
import android.os.Bundle
import android.widget.Button
import androidx.appcompat.app.AppCompatActivity

/**
 * Launcher screen: a simple picker between the two "Pay a parking ticket" ground-truth screens
 * below. Not itself part of the ground truth -- see ../../../../../ground-truth.views.json and
 * ground-truth.compose.json.
 */
class MainActivity : AppCompatActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        findViewById<Button>(R.id.buttonViews).setOnClickListener {
            startActivity(Intent(this, ViewsActivity::class.java))
        }
        findViewById<Button>(R.id.buttonCompose).setOnClickListener {
            startActivity(Intent(this, ComposeActivity::class.java))
        }
    }
}
