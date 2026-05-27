package com.aguiabranca.app.core.ui.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

private data class Rule(val label: String, val value: String)

private val pointRules = listOf(
    Rule("Submeter uma ideia", "+10 pts"),
    Rule("Ideia conectada a uma orientação estratégica", "+15 pts"),
    Rule("Ideia aprovada pela curadoria", "+50 pts"),
    Rule("Projeto originado da ideia é concluído", "+200 pts"),
)

private val badgeRules = listOf(
    Rule("Primeira Ideia", "1ª ideia submetida"),
    Rule("Estrategista", "1 ideia aprovada conectada à estratégia"),
    Rule("Inovador do Mês", "5+ ideias em um mesmo mês"),
    Rule("Impacto Real", "1 ideia implementada"),
    Rule("Visionário", "ideias aprovadas em 3 orientações diferentes"),
)

@Composable
fun PointsRulesCard(modifier: Modifier = Modifier) {
    Surface(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        color = MaterialTheme.colorScheme.surface
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text("Como ganhar pontos", fontWeight = FontWeight.SemiBold)
            Spacer(Modifier.height(4.dp))
            Text(
                "Os pontos são creditados automaticamente em cada etapa do ciclo de inovação.",
                fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Spacer(Modifier.height(12.dp))
            pointRules.forEach { rule ->
                Row(
                    modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(rule.label, fontSize = 13.sp, modifier = Modifier.weight(1f))
                    Text(rule.value, fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.primary)
                }
            }

            Spacer(Modifier.height(12.dp))
            HorizontalDivider()
            Spacer(Modifier.height(12.dp))

            Text("Como desbloquear badges", fontWeight = FontWeight.SemiBold)
            Spacer(Modifier.height(8.dp))
            badgeRules.forEach { rule ->
                Row(
                    modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(rule.label, fontSize = 13.sp, fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(0.4f))
                    Text(rule.value, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.weight(0.6f))
                }
            }
        }
    }
}
