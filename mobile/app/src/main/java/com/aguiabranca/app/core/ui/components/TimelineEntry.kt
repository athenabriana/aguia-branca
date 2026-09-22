package com.aguiabranca.app.core.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.aguiabranca.app.core.domain.model.FieldChange
import com.aguiabranca.app.core.domain.model.ProjectUpdate
import com.aguiabranca.app.core.ui.theme.NeutralGray300
import com.aguiabranca.app.core.ui.theme.NeutralGray500

@Composable
fun TimelineEntry(update: ProjectUpdate, isLast: Boolean = false, modifier: Modifier = Modifier) {
    Row(modifier = modifier.fillMaxWidth()) {
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            Box(
                modifier = Modifier
                    .size(14.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.primary)
            )
            if (!isLast) {
                Box(
                    modifier = Modifier
                        .width(2.dp)
                        .height(80.dp)
                        .background(NeutralGray300)
                )
            }
        }
        Spacer(Modifier.width(12.dp))
        Surface(
            color = MaterialTheme.colorScheme.surface,
            shape = RoundedCornerShape(12.dp),
            modifier = Modifier.fillMaxWidth()
        ) {
            Column(modifier = Modifier.padding(12.dp)) {
                Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                    Text(update.authorName, fontWeight = FontWeight.SemiBold, fontSize = 14.sp)
                    Spacer(Modifier.width(8.dp))
                    Text(formatDateShort(update.createdAt), color = NeutralGray500, fontSize = 12.sp)
                }
                if (update.note.isNotBlank()) {
                    Spacer(Modifier.height(4.dp))
                    Text(update.note, fontSize = 13.sp)
                }
                if (update.changes.isNotEmpty()) {
                    Spacer(Modifier.height(8.dp))
                    update.changes.forEach { change ->
                        Text(
                            text = formatChange(change),
                            fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
                Spacer(Modifier.height(8.dp))
            }
        }
    }
}

private fun formatChange(change: FieldChange): String {
    val labels = mapOf(
        "title" to "Título",
        "description" to "Descrição",
        "stage" to "Etapa",
        "statusText" to "Status",
        "investment" to "Investimento",
        "financialReturn" to "Retorno financeiro",
        "productivityGain" to "Ganho produtividade",
        "costReduction" to "Redução de custo",
        "guidelineId" to "Orientação",
        "targetDate" to "Prazo"
    )
    val label = labels[change.field] ?: change.field
    val from = formatValue(change.field, change.from)
    val to = formatValue(change.field, change.to)
    return "$label: $from → $to"
}

private fun formatValue(field: String, value: Any?): String {
    if (value == null) return "—"
    return when (field) {
        "investment", "financialReturn", "costReduction" ->
            formatCurrency((value as? Number)?.toDouble() ?: 0.0)
        "productivityGain" -> formatPercent((value as? Number)?.toDouble() ?: 0.0)
        "targetDate" -> formatDateShort((value as? Number)?.toLong())
        else -> value.toString()
    }
}
