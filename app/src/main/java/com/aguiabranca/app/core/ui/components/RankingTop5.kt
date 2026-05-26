package com.aguiabranca.app.core.ui.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.aguiabranca.app.core.domain.model.User

@Composable
fun RankingTop5(users: List<User>, highlightUid: String? = null, modifier: Modifier = Modifier) {
    Surface(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        color = MaterialTheme.colorScheme.surface
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text("🏆 Top 5 inovadores do mês", fontWeight = FontWeight.SemiBold)
            Spacer(Modifier.padding(4.dp))
            if (users.isEmpty()) {
                Text("Sem pontuações ainda este mês.", fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            } else {
                users.take(5).forEachIndexed { idx, u ->
                    val highlight = u.id == highlightUid
                    Row(
                        modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            "${idx + 1}. ${u.name}",
                            fontWeight = if (highlight) FontWeight.Bold else FontWeight.Normal,
                            color = if (highlight) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurface
                        )
                        Text("${u.points} pts", fontWeight = FontWeight.SemiBold)
                    }
                }
            }
        }
    }
}
