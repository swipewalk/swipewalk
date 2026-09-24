package org.swipewalk.nativeandroid

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Help
import androidx.compose.material.icons.filled.Call
import androidx.compose.material.icons.filled.Email
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextField
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/**
 * "Pay a parking ticket", built with Jetpack Compose. Ground truth for Swipewalk -- see
 * ground-truth.compose.json and README.md, both in this sample's root folder. Bugs are
 * deliberate and tagged N1..N8 in the composables below, matching the JSON file; composables
 * tagged OK are negative controls.
 */
class ComposeActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            MaterialTheme {
                ParkingTicketScreen()
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ParkingTicketScreen() {
    Scaffold(
        topBar = { TopAppBar(title = { Text(stringResource(R.string.compose_screen_title)) }) }
    ) { padding ->
        Column(
            modifier = Modifier
                .padding(padding)
                .background(Color.White)
                .verticalScroll(rememberScrollState())
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            Text(
                text = stringResource(R.string.screen_heading),
                fontSize = 20.sp,
                color = Color(0xFF1F1F1F)
            )

            // N1: icon-only button; contentDescription = null on the button's only content, the
            // classic Compose mistake of treating the icon as decorative when it IS the control.
            IconButton(onClick = { /* search */ }) {
                Icon(imageVector = Icons.Filled.Search, contentDescription = null)
            }

            // N2: low-contrast helper text (#AAAAAA on white, about 2.3:1).
            Text(
                text = stringResource(R.string.helper_text),
                fontSize = 14.sp,
                color = Color(0xFFAAAAAA)
            )

            // N7: converting a dp value to sp bypasses the system font-scale setting entirely,
            // unlike a plain 14.sp literal (which would scale). Only shows up on Swipewalk's
            // large-text rescan -- see androidLargeText in ground-truth.compose.json.
            Text(
                text = stringResource(R.string.late_fees_text),
                fontSize = with(LocalDensity.current) { 14.dp.toSp() },
                color = Color(0xFF1F1F1F)
            )

            // N6: bare TextField -- no label, no contentDescription -- with a plain Text caption
            // above it that is not programmatically associated with the field.
            var plateNumber by remember { mutableStateOf("") }
            Text(
                text = stringResource(R.string.plate_number_label),
                fontSize = 14.sp,
                color = Color(0xFF1F1F1F)
            )
            TextField(
                value = plateNumber,
                onValueChange = { plateNumber = it },
                modifier = Modifier.fillMaxWidth()
            )

            // OK3: TextField with a correctly associated label.
            var ticketNumber by remember { mutableStateOf("") }
            TextField(
                value = ticketNumber,
                onValueChange = { ticketNumber = it },
                label = { Text(stringResource(R.string.ticket_number_label)) },
                modifier = Modifier.fillMaxWidth()
            )

            // N3: icon button with a correct accessible name, but forced down to a 20dp touch
            // target -- well under Android's 48dp guideline. (IconButton's own minimum-touch-target
            // handling can still pad this back up in practice; same caveat as BuggyApp's B5.)
            IconButton(onClick = { /* info */ }, modifier = Modifier.size(20.dp)) {
                Icon(
                    imageVector = Icons.Filled.Info,
                    contentDescription = stringResource(R.string.more_info_description)
                )
            }

            // N8: icon button with a correct name and normal (default) touch target size, but a
            // very light tint -- a contrast bug, not a naming bug.
            IconButton(onClick = { /* help */ }) {
                Icon(
                    imageVector = Icons.AutoMirrored.Filled.Help,
                    contentDescription = stringResource(R.string.help_description),
                    tint = Color(0xFFDDDDDD)
                )
            }

            // N5: visible text "Pay" but the accessible name is "Submit" (label-in-name mismatch).
            Button(
                onClick = { /* pay */ },
                modifier = Modifier.semantics { contentDescription = "Submit" }
            ) {
                Text(stringResource(R.string.pay_button_text))
            }

            // N4: accessible name copied from a developer identifier.
            IconButton(onClick = { /* email receipt */ }) {
                Icon(imageVector = Icons.Filled.Email, contentDescription = "img_btn_email_receipt")
            }

            // OK2: icon button with a correct contentDescription; forced to 44dp, a realistic
            // near-miss below Android's 48dp guideline -- not a planted bug, but honestly may
            // still trip the target-size platform advisory (documented in
            // ground-truth.compose.json).
            IconButton(onClick = { /* call */ }, modifier = Modifier.size(44.dp)) {
                Icon(
                    imageVector = Icons.Filled.Call,
                    contentDescription = stringResource(R.string.call_description)
                )
            }

            // OK1: normal button, visible text matches its accessible name.
            Button(onClick = { /* history */ }) {
                Text(stringResource(R.string.history_button_text))
            }
        }
    }
}
