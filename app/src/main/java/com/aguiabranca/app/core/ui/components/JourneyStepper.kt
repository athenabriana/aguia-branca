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
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.aguiabranca.app.core.domain.model.IdeaStatus
import com.aguiabranca.app.core.ui.theme.NeutralGray300
import com.aguiabranca.app.core.ui.theme.NeutralGray500
import com.aguiabranca.app.core.ui.theme.SemanticDanger
import com.aguiabranca.app.core.ui.theme.SemanticSuccess

enum class JourneyStage(val label: String) {
    SUBMETIDA("Submetida"),
    EM_ANALISE("Em análise"),
    APROVADA("Aprovada"),
    EM_EXECUCAO("Em execução"),
    RESULTADO("Resultado obtido")
}

@Composable
fun JourneyStepper(
    currentStage: JourneyStage?,
    dates: Map<JourneyStage, Long?>,
    status: IdeaStatus,
    rejectionComment: String? = null,
    modifier: Modifier = Modifier
) {
    if (status == IdeaStatus.REJEITADA) {
        Surface(
            modifier = modifier.fillMaxWidth(),
            color = SemanticDanger.copy(alpha = 0.12f),
            shape = RoundedCornerShape(12.dp)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                Text(
                    text = "❌ Ideia rejeitada",
                    color = SemanticDanger,
                    fontWeight = FontWeight.SemiBold
                )
                rejectionComment?.takeIf { it.isNotBlank() }?.let {
                    Spacer(Modifier.height(8.dp))
                    Text(it, color = MaterialTheme.colorScheme.onSurface)
                }
            }
        }
        return
    }

    Column(modifier = modifier.fillMaxWidth()) {
        val stages = JourneyStage.entries
        val currentIndex = currentStage?.let { stages.indexOf(it) } ?: -1
        stages.forEachIndexed { idx, stage ->
            val reached = idx <= currentIndex
            Row(verticalAlignment = Alignment.Top) {
                Column(horizontalAlignment = Alignment.CenterHorizontally) {
                    Box(
                        modifier = Modifier
                            .size(24.dp)
                            .clip(CircleShape)
                            .background(if (reached) SemanticSuccess else NeutralGray300),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(if (reached) "✓" else "○", color = Color.White, fontSize = androidx.compose.ui.unit.TextUnit.Unspecified)
                    }
                    if (idx < stages.lastIndex) {
                        Box(
                            modifier = Modifier
                                .width(2.dp)
                                .height(28.dp)
                                .background(if (reached) SemanticSuccess else NeutralGray300)
                        )
                    }
                }
                Spacer(Modifier.width(12.dp))
                Column(modifier = Modifier.padding(top = 2.dp)) {
                    Text(
                        text = stage.label,
                        fontWeight = if (reached) FontWeight.SemiBold else FontWeight.Normal,
                        color = if (reached) MaterialTheme.colorScheme.onSurface else NeutralGray500
                    )
                    val date = dates[stage]
                    if (reached && date != null && date > 0) {
                        Text(formatDateShort(date), color = NeutralGray500, fontWeight = FontWeight.Normal)
                    }
                    Spacer(Modifier.height(16.dp))
                }
            }
        }
    }
}
