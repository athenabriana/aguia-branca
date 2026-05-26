package com.aguiabranca.app.feature.ideas.ui

import androidx.compose.foundation.clickable
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
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExtendedFloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import com.aguiabranca.app.core.ui.components.GuidelineBadge
import com.aguiabranca.app.core.ui.components.NavTab
import com.aguiabranca.app.core.ui.components.RankingTop5
import com.aguiabranca.app.core.ui.components.RoleScaffold
import com.aguiabranca.app.core.ui.components.StatusBadge
import com.aguiabranca.app.core.ui.components.formatDateShort
import com.aguiabranca.app.core.ui.local.LocalSession
import com.aguiabranca.app.feature.profile.ui.UsersRankingViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MyIdeasScreen(
    onOpenIdea: (String) -> Unit,
    onNewIdea: () -> Unit,
    onTab: (NavTab) -> Unit,
    vm: MyIdeasViewModel = hiltViewModel(),
    rankingVm: UsersRankingViewModel = hiltViewModel()
) {
    val session = LocalSession.current ?: return
    LaunchedEffect(session.uid) { vm.setAuthor(session.uid) }
    val items by vm.ideas.collectAsState()
    val ranking by rankingVm.ranking.collectAsState()

    RoleScaffold(
        role = session.role,
        currentTab = NavTab.IDEAS,
        onTabClick = onTab,
        topBar = {
            TopAppBar(title = { Text("Minhas ideias") })
        }
    ) { padding ->
        Box(modifier = Modifier.fillMaxSize().padding(padding)) {
            LazyColumn(
                modifier = Modifier.fillMaxSize(),
                contentPadding = PaddingValues(16.dp)
            ) {
                item { RankingTop5(ranking, highlightUid = session.uid); Spacer(Modifier.height(12.dp)) }
                if (items.isEmpty()) {
                    item {
                        Text(
                            "Você ainda não cadastrou ideias. Toque em + para começar.",
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
                items(items, key = { it.id }) { idea ->
                    Surface(
                        shape = RoundedCornerShape(14.dp),
                        color = MaterialTheme.colorScheme.surface,
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(vertical = 6.dp)
                            .clickable { onOpenIdea(idea.id) }
                    ) {
                        Column(modifier = Modifier.padding(16.dp)) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Text(idea.title, fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(1f))
                                StatusBadge(idea.status)
                            }
                            Spacer(Modifier.height(4.dp))
                            Text("submetida em ${formatDateShort(idea.createdAt)}", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            if (idea.guidelineId != null) {
                                Spacer(Modifier.height(6.dp))
                                GuidelineBadge(title = "Conexão estratégica")
                            }
                        }
                    }
                }
            }
            ExtendedFloatingActionButton(
                onClick = onNewIdea,
                icon = { Icon(Icons.Outlined.Add, contentDescription = null) },
                text = { Text("Nova ideia") },
                modifier = Modifier.align(Alignment.BottomEnd).padding(16.dp)
            )
        }
    }
}
