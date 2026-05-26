package com.aguiabranca.app.core.ui.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Slider
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.aguiabranca.app.core.domain.model.Ice

@Composable
fun IceMatrix(
    initial: Ice?,
    onSave: (Ice) -> Unit,
    modifier: Modifier = Modifier,
    saveLabel: String = "Salvar avaliação"
) {
    var impact by remember { mutableIntStateOf(initial?.impact ?: 5) }
    var confidence by remember { mutableIntStateOf(initial?.confidence ?: 5) }
    var ease by remember { mutableIntStateOf(initial?.ease ?: 5) }
    val ice = Ice(impact, confidence, ease)

    Surface(
        modifier = modifier.fillMaxWidth(),
        color = MaterialTheme.colorScheme.surface,
        shape = RoundedCornerShape(16.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text("Matriz ICE", fontWeight = FontWeight.SemiBold)
            Spacer(Modifier.height(12.dp))
            iceSlider("Impacto", impact) { impact = it }
            iceSlider("Confiança", confidence) { confidence = it }
            iceSlider("Facilidade", ease) { ease = it }
            Spacer(Modifier.height(12.dp))
            Text(
                "Score: ${ice.score}",
                fontWeight = FontWeight.Bold,
                fontSize = 28.sp,
                color = MaterialTheme.colorScheme.primary
            )
            Spacer(Modifier.height(12.dp))
            Button(
                enabled = ice.isComplete,
                onClick = { onSave(ice) },
                modifier = Modifier.fillMaxWidth()
            ) { Text(saveLabel) }
        }
    }
}

@Composable
private fun iceSlider(label: String, value: Int, onChange: (Int) -> Unit) {
    Column {
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            Text(label, fontWeight = FontWeight.Medium)
            Text("$value / 10", fontWeight = FontWeight.SemiBold)
        }
        Slider(
            value = value.toFloat(),
            onValueChange = { onChange(it.toInt().coerceIn(1, 10)) },
            valueRange = 1f..10f,
            steps = 8,
            modifier = Modifier.fillMaxWidth()
        )
    }
}
