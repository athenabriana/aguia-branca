package com.aguiabranca.app.feature.dashboard.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.aguiabranca.app.core.domain.error.toPtBr
import com.aguiabranca.app.core.domain.model.DashboardFilters
import com.aguiabranca.app.core.domain.model.InsightPriority
import com.aguiabranca.app.core.domain.model.Insights
import com.aguiabranca.app.core.ui.components.Pill
import com.aguiabranca.app.core.ui.state.UiState
import com.aguiabranca.app.core.ui.theme.SemanticDanger
import com.aguiabranca.app.core.ui.theme.SemanticInfo
import com.aguiabranca.app.core.ui.theme.SemanticWarning
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

const val AI_DISCLAIMER = "Gerado por IA — valide antes de decidir"

/**
 * "✨ Insights da IA": análise executiva do dashboard (destaques, riscos e recomendações priorizadas) gerada no servidor.
 * Respeita os filtros ativos, pode ser ocultado (modo apresentação) e trata falhas sem inventar conteúdo.
 */
@Composable
fun InsightsCard(
    ui: InsightsUi,
    filters: DashboardFilters,
    guidelineTitles: Map<String, String>,
    onGenerate: (refresh: Boolean) -> Unit,
    modifier: Modifier = Modifier
) {
    var visible by rememberSaveable { mutableStateOf(true) }

    Surface(shape = RoundedCornerShape(16.dp), color = MaterialTheme.colorScheme.surface, modifier = modifier.fillMaxWidth()) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text("✨ Insights da IA", fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(1f))
                TextButton(onClick = { visible = !visible }) { Text(if (visible) "Ocultar" else "Mostrar") }
            }
            if (!visible) return@Column

            Pill(AI_DISCLAIMER, bg = SemanticInfo.copy(alpha = 0.12f), fg = SemanticInfo)

            when (val s = ui.state) {
                UiState.Idle -> {
                    Text(
                        "Gere uma análise dos resultados exibidos (período e divisão selecionados), com destaques, riscos e recomendações.",
                        fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    Button(onClick = { onGenerate(false) }, modifier = Modifier.fillMaxWidth()) { Text("✨ Gerar insights") }
                }
                UiState.Loading -> Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                    CircularProgressIndicator(modifier = Modifier.size(24.dp), strokeWidth = 2.dp)
                    Text("Analisando os dados… pode levar alguns segundos.", fontSize = 13.sp)
                }
                is UiState.Error -> {
                    Text(s.error.toPtBr(), color = MaterialTheme.colorScheme.error, fontSize = 14.sp)
                    OutlinedButton(onClick = { onGenerate(false) }) { Text("Tentar novamente") }
                }
                is UiState.Success -> InsightsContent(
                    insights = s.data, stale = ui.generatedFor != null && ui.generatedFor != filters,
                    guidelineTitles = guidelineTitles, onGenerate = onGenerate
                )
            }
        }
    }
}

@Composable
private fun InsightsContent(insights: Insights, stale: Boolean, guidelineTitles: Map<String, String>, onGenerate: (Boolean) -> Unit) {
    if (stale) {
        Text("Os filtros mudaram desde que estes insights foram gerados.", color = SemanticWarning, fontSize = 12.sp)
    }
    Text(insights.summary, fontSize = 14.sp)

    Section("Destaques", insights.highlights)
    Section("Riscos", insights.risks)

    if (insights.recommendations.isNotEmpty()) {
        Text("Recomendações", fontWeight = FontWeight.SemiBold, fontSize = 13.sp)
        insights.recommendations.forEach { r ->
            Column(verticalArrangement = Arrangement.spacedBy(4.dp), modifier = Modifier.padding(vertical = 4.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    PriorityChip(r.priority)
                    Text(r.title, fontWeight = FontWeight.SemiBold, fontSize = 14.sp, modifier = Modifier.weight(1f))
                }
                Text(r.detail, fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                r.relatedGuidelineId?.let { id ->
                    Text("Orientação: ${guidelineTitles[id] ?: "vinculada"}", fontSize = 12.sp, color = MaterialTheme.colorScheme.primary)
                }
            }
        }
    }

    Spacer(Modifier.height(2.dp))
    Text(
        buildString {
            append("Gerado em ").append(formatGeneratedAt(insights.generatedAt))
            if (insights.fromCache) append(" · resultado em cache")
            if (insights.model.isNotBlank()) append(" · ").append(insights.model)
        },
        fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant
    )
    OutlinedButton(onClick = { onGenerate(true) }) { Text(if (stale) "Gerar para os filtros atuais" else "Atualizar") }
}

@Composable
private fun Section(title: String, items: List<String>) {
    if (items.isEmpty()) return
    Text(title, fontWeight = FontWeight.SemiBold, fontSize = 13.sp)
    items.forEach { Text("• $it", fontSize = 13.sp) }
}

@Composable
fun PriorityChip(priority: InsightPriority) {
    val (label, color) = when (priority) {
        InsightPriority.ALTA -> "ALTA" to SemanticDanger
        InsightPriority.MEDIA -> "MÉDIA" to SemanticWarning
        InsightPriority.BAIXA -> "BAIXA" to SemanticInfo
    }
    Pill(label, bg = color.copy(alpha = 0.14f), fg = color)
}

internal fun formatGeneratedAt(epoch: Long): String =
    if (epoch <= 0) "—" else SimpleDateFormat("dd/MM/yyyy 'às' HH:mm", Locale("pt", "BR")).format(Date(epoch))
