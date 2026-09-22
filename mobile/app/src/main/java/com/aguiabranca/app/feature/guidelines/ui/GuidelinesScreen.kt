package com.aguiabranca.app.feature.guidelines.ui

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.Delete
import androidx.compose.material.icons.outlined.Edit
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExtendedFloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.ui.components.NavTab
import com.aguiabranca.app.core.ui.components.PillarChip
import com.aguiabranca.app.core.ui.components.RoleScaffold
import com.aguiabranca.app.core.ui.components.formatDateShort
import com.aguiabranca.app.core.ui.local.LocalSession

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun GuidelinesScreen(
    onAdmin: () -> Unit,
    onEdit: (String) -> Unit,
    onTab: (NavTab) -> Unit,
    vm: GuidelinesViewModel = hiltViewModel()
) {
    val session = LocalSession.current ?: return
    val items by vm.guidelines.collectAsState()
    var deleteId by remember { mutableStateOf<String?>(null) }

    RoleScaffold(
        role = session.role,
        currentTab = NavTab.GUIDELINES,
        onTabClick = onTab,
        topBar = { TopAppBar(title = { Text("Orientações estratégicas") }) }
    ) { padding ->
        Box(modifier = Modifier.fillMaxSize().padding(padding)) {
            if (items.isEmpty()) {
                Column(
                    modifier = Modifier.fillMaxSize().padding(32.dp),
                    verticalArrangement = androidx.compose.foundation.layout.Arrangement.Center,
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    Text("Nenhuma orientação cadastrada", fontWeight = FontWeight.SemiBold, fontSize = 16.sp)
                    Spacer(Modifier.size(8.dp))
                    Text(
                        if (session.role == Role.LIDER)
                            "Use o botão Nova para definir as orientações estratégicas que guiarão as ideias e projetos."
                        else
                            "As orientações estratégicas aparecem aqui quando publicadas pelas lideranças.",
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        fontSize = 13.sp,
                        textAlign = androidx.compose.ui.text.style.TextAlign.Center
                    )
                }
            } else {
                LazyColumn(modifier = Modifier.fillMaxSize(), contentPadding = PaddingValues(16.dp)) {
                    items(items, key = { it.id }) { g ->
                        Surface(
                            modifier = Modifier.fillMaxWidth().padding(vertical = 6.dp),
                            shape = RoundedCornerShape(14.dp),
                            color = MaterialTheme.colorScheme.surface
                        ) {
                            Column(modifier = Modifier.padding(16.dp)) {
                                Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                                    Text(g.title, fontWeight = FontWeight.SemiBold, fontSize = 16.sp, modifier = Modifier.weight(1f))
                                    if (session.role == Role.LIDER) {
                                        IconButton(onClick = { onEdit(g.id) }) { Icon(Icons.Outlined.Edit, contentDescription = "Editar") }
                                        IconButton(onClick = { deleteId = g.id }) { Icon(Icons.Outlined.Delete, contentDescription = "Excluir") }
                                    }
                                }
                                Spacer(Modifier.size(4.dp))
                                Text(g.description, fontSize = 14.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                                Spacer(Modifier.size(8.dp))
                                Row(verticalAlignment = Alignment.CenterVertically) {
                                    PillarChip(g.pillar)
                                    Spacer(Modifier.size(8.dp))
                                    Text("• ${g.authorName}", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                                    Spacer(Modifier.size(8.dp))
                                    Text("atualizada em ${formatDateShort(g.updatedAt)}", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                                }
                            }
                        }
                    }
                }
            }
            if (session.role == Role.LIDER) {
                ExtendedFloatingActionButton(
                    onClick = onAdmin,
                    icon = { Icon(Icons.Outlined.Add, contentDescription = "Nova orientação") },
                    text = { Text("Nova") },
                    modifier = Modifier.align(Alignment.BottomEnd).padding(16.dp)
                )
            }
        }
    }

    deleteId?.let { id ->
        AlertDialog(
            onDismissRequest = { deleteId = null },
            title = { Text("Excluir orientação?") },
            text = { Text("Esta ação é permanente. Ideias vinculadas mostrarão \"Orientação removida\".") },
            confirmButton = {
                TextButton(onClick = { vm.delete(id) { deleteId = null } }) { Text("Excluir") }
            },
            dismissButton = { TextButton(onClick = { deleteId = null }) { Text("Cancelar") } }
        )
    }
}
