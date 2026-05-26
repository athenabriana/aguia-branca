package com.aguiabranca.app.feature.profile.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.CenterAlignedTopAppBar
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import com.aguiabranca.app.core.domain.badge.Badges
import com.aguiabranca.app.core.domain.model.IdeaStatus
import com.aguiabranca.app.core.ui.components.BadgeChip
import com.aguiabranca.app.core.ui.local.LocalSession

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ProfileScreen(
    onBack: () -> Unit,
    vm: ProfileViewModel = hiltViewModel()
) {
    val session = LocalSession.current ?: return
    LaunchedEffect(session.uid) { vm.setUid(session.uid) }
    val ui by vm.ui.collectAsState()
    val user = ui.user

    Scaffold(
        topBar = {
            CenterAlignedTopAppBar(
                title = { Text("Perfil") },
                navigationIcon = { TextButton(onClick = onBack) { Text("Voltar") } }
            )
        }
    ) { padding ->
        Column(
            modifier = Modifier.fillMaxSize().padding(padding).padding(16.dp).verticalScroll(rememberScrollState())
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    modifier = Modifier.size(64.dp).clip(CircleShape).background(MaterialTheme.colorScheme.primary),
                    contentAlignment = Alignment.Center
                ) {
                    Text((user?.name?.firstOrNull()?.uppercase() ?: "?"), color = Color.White, fontSize = 24.sp, fontWeight = FontWeight.Bold)
                }
                Spacer(Modifier.size(16.dp))
                Column {
                    Text(user?.name ?: session.name, fontWeight = FontWeight.Bold, fontSize = 18.sp)
                    Text("${user?.role?.name ?: session.role.name} · ${user?.division?.name ?: session.division.name}", color = MaterialTheme.colorScheme.onSurfaceVariant, fontSize = 12.sp)
                }
            }
            Spacer(Modifier.height(20.dp))

            Surface(shape = RoundedCornerShape(20.dp), color = MaterialTheme.colorScheme.primary, modifier = Modifier.fillMaxWidth()) {
                Column(modifier = Modifier.padding(20.dp)) {
                    Text("Pontos totais", color = Color.White.copy(alpha = 0.85f), fontSize = 13.sp)
                    Text("${user?.points ?: 0}", color = Color.White, fontWeight = FontWeight.Black, fontSize = 36.sp)
                }
            }
            Spacer(Modifier.height(20.dp))

            Text("Badges", fontWeight = FontWeight.SemiBold)
            Spacer(Modifier.height(8.dp))
            val allBadges = listOf(Badges.PRIMEIRA_IDEIA, Badges.ESTRATEGISTA, Badges.INOVADOR_MES, Badges.IMPACTO_REAL, Badges.VISIONARIO)
            LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp), contentPadding = PaddingValues(vertical = 4.dp)) {
                items(items = allBadges, key = { it }) { name ->
                    BadgeChip(name = name, unlocked = (user?.badges ?: emptyList()).contains(name))
                }
            }
            Spacer(Modifier.height(20.dp))

            Text("Minhas ideias por status", fontWeight = FontWeight.SemiBold)
            Spacer(Modifier.height(8.dp))
            IdeaStatus.entries.forEach { s ->
                Row(modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text(when (s) {
                        IdeaStatus.SUBMETIDA -> "Submetidas"
                        IdeaStatus.EM_ANALISE -> "Em análise"
                        IdeaStatus.APROVADA -> "Aprovadas"
                        IdeaStatus.REJEITADA -> "Rejeitadas"
                        IdeaStatus.IMPLEMENTADA -> "Implementadas"
                    })
                    Text((ui.ideasByStatus[s] ?: 0).toString(), fontWeight = FontWeight.SemiBold)
                }
            }
            Spacer(Modifier.height(28.dp))
            OutlinedButton(onClick = { vm.logout(onBack) }, modifier = Modifier.fillMaxWidth()) { Text("Sair") }
            Spacer(Modifier.height(24.dp))
        }
    }
}

