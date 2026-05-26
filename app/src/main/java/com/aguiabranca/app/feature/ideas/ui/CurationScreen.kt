package com.aguiabranca.app.feature.ideas.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
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
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import com.aguiabranca.app.core.ui.components.NavTab
import com.aguiabranca.app.core.ui.components.RoleScaffold
import com.aguiabranca.app.core.ui.components.StatusBadge
import com.aguiabranca.app.core.ui.local.LocalSession

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CurationScreen(
    onOpenIdea: (String) -> Unit,
    onTab: (NavTab) -> Unit,
    vm: CurationViewModel = hiltViewModel()
) {
    val session = LocalSession.current ?: return
    val raw by vm.ideas.collectAsState()
    val ideas = vm.sorted(raw)

    RoleScaffold(
        role = session.role,
        currentTab = NavTab.CURATION,
        onTabClick = onTab,
        topBar = { TopAppBar(title = { Text("Curadoria") }) }
    ) { padding ->
        if (ideas.isEmpty()) {
            Column(
                modifier = Modifier.fillMaxSize().padding(padding).padding(32.dp),
                verticalArrangement = Arrangement.Center,
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Text(
                    "Nenhuma ideia para curar",
                    fontWeight = FontWeight.SemiBold,
                    fontSize = 16.sp
                )
                Spacer(Modifier.height(8.dp))
                Text(
                    "Ideias submetidas pelos operadores aparecem aqui para avaliação com a matriz ICE.",
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    fontSize = 13.sp,
                    textAlign = androidx.compose.ui.text.style.TextAlign.Center
                )
            }
        } else {
            LazyColumn(modifier = Modifier.fillMaxSize().padding(padding), contentPadding = PaddingValues(16.dp)) {
                items(ideas, key = { it.id }) { idea ->
                    Surface(
                        shape = RoundedCornerShape(14.dp),
                        color = MaterialTheme.colorScheme.surface,
                        modifier = Modifier.fillMaxWidth().padding(vertical = 6.dp).clickable { onOpenIdea(idea.id) }
                    ) {
                        Column(modifier = Modifier.padding(16.dp)) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Text(idea.title, fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(1f))
                                StatusBadge(idea.status)
                            }
                            Spacer(Modifier.height(4.dp))
                            Text("Autor: ${idea.authorName}", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            Spacer(Modifier.height(6.dp))
                            val score = idea.ice?.score
                            if (score != null) {
                                Text("ICE score: $score", fontWeight = FontWeight.SemiBold, color = MaterialTheme.colorScheme.primary)
                            } else {
                                Text("Aguardando avaliação", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                        }
                    }
                }
            }
        }
    }
}
