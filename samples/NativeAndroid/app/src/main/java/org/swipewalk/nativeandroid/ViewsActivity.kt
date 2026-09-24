package org.swipewalk.nativeandroid

import android.os.Bundle
import android.widget.Button
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity

/**
 * "Pay a parking ticket", built with the classic Android View system. Ground truth for
 * Swipewalk -- see ground-truth.views.json and README.md, both in this sample's root folder.
 * Bugs are deliberate and tagged N1..N8 in res/layout/activity_views.xml, matching the JSON file;
 * elements tagged OK there are negative controls.
 */
class ViewsActivity : AppCompatActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_views)
        supportActionBar?.title = getString(R.string.views_screen_title)

        findViewById<Button>(R.id.buttonHistory).setOnClickListener {
            Toast.makeText(this, R.string.history_button_text, Toast.LENGTH_SHORT).show()
        }
    }
}
