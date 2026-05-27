package com.aguiabranca.app.core.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.aguiabranca.app.core.domain.model.IdeaStatus
import com.aguiabranca.app.core.domain.model.Pillar
import com.aguiabranca.app.core.domain.model.ProjectStage
import com.aguiabranca.app.core.ui.theme.SemanticDanger
import com.aguiabranca.app.core.ui.theme.SemanticInfo
import com.aguiabranca.app.core.ui.theme.SemanticSuccess
import com.aguiabranca.app.core.ui.theme.SemanticWarning
import java.text.NumberFormat
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

@Composable
fun StatusBadge(status: IdeaStatus) {
    val (label, color) = when (status) {
        IdeaStatus.SUBMETIDA -> "Submetida" to SemanticInfo
        IdeaStatus.EM_ANALISE -> "Em análise" to SemanticWarning
        IdeaStatus.APROVADA -> "Aprovada" to SemanticSuccess
        IdeaStatus.REJEITADA -> "Rejeitada" to SemanticDanger
        IdeaStatus.IMPLEMENTADA -> "Implementada" to SemanticSuccess
    }
    Pill(text = label, bg = color.copy(alpha = 0.15f), fg = color)
}

@Composable
fun StageBadge(stage: ProjectStage) {
    val (label, color) = when (stage) {
        ProjectStage.PLANEJAMENTO -> "Planejamento" to SemanticInfo
        ProjectStage.EM_EXECUCAO -> "Em execução" to SemanticWarning
        ProjectStage.CONCLUIDO -> "Concluído" to SemanticSuccess
        ProjectStage.CANCELADO -> "Cancelado" to SemanticDanger
    }
    Pill(text = label, bg = color.copy(alpha = 0.15f), fg = color)
}

@Composable
fun GuidelineBadge(title: String?, modifier: Modifier = Modifier) {
    val text = title?.let { "Conectada com: $it" } ?: "Orientação removida"
    val fg = if (title == null) SemanticDanger else MaterialTheme.colorScheme.primary
    Pill(text = text, bg = fg.copy(alpha = 0.10f), fg = fg, modifier = modifier)
}

@Composable
fun PillarChip(pillar: Pillar) {
    val label = when (pillar) {
        Pillar.DIRECIONAMENTO -> "Direcionamento"
        Pillar.IDEIAS -> "Ideias"
        Pillar.PROJETOS -> "Projetos"
        Pillar.MENSURACAO -> "Mensuração"
    }
    Pill(text = label, bg = MaterialTheme.colorScheme.secondary.copy(alpha = 0.25f), fg = MaterialTheme.colorScheme.onSurface)
}

@Composable
fun BadgeChip(name: String, unlocked: Boolean) {
    val color = if (unlocked) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outline
    val emoji = when (name) {
        "Primeira Ideia" -> "🌱"
        "Estrategista" -> "🎯"
        "Inovador do Mês" -> "🚀"
        "Impacto Real" -> "💥"
        "Visionário" -> "🔭"
        else -> "🏅"
    }
    Pill(
        text = "$emoji  $name",
        bg = color.copy(alpha = 0.15f),
        fg = color
    )
}

@Composable
fun Pill(text: String, bg: Color, fg: Color, modifier: Modifier = Modifier) {
    Surface(
        modifier = modifier,
        color = bg,
        shape = RoundedCornerShape(50)
    ) {
        Text(
            text = text,
            color = fg,
            fontWeight = FontWeight.SemiBold,
            fontSize = 12.sp,
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp)
        )
    }
}

fun formatCurrency(value: Double): String {
    if (value.isNaN() || value.isInfinite()) return "—"
    val nf = NumberFormat.getCurrencyInstance(Locale("pt", "BR"))
    nf.maximumFractionDigits = 0
    return nf.format(value)
}

fun formatPercent(value: Double): String {
    if (value.isNaN() || value.isInfinite()) return "—"
    val nf = NumberFormat.getNumberInstance(Locale("pt", "BR"))
    nf.minimumFractionDigits = 1
    nf.maximumFractionDigits = 1
    return nf.format(value) + "%"
}

fun formatDate(epoch: Long?): String {
    if (epoch == null || epoch == 0L) return "—"
    val sdf = SimpleDateFormat("dd/MM/yyyy", Locale("pt", "BR"))
    return sdf.format(Date(epoch))
}

fun formatDateShort(epoch: Long?): String {
    if (epoch == null || epoch == 0L) return "—"
    val sdf = SimpleDateFormat("dd/MM/yy", Locale("pt", "BR"))
    return sdf.format(Date(epoch))
}
