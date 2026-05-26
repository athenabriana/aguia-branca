package com.aguiabranca.app.feature.guidelines.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
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
import androidx.compose.material3.CenterAlignedTopAppBar
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExtendedFloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
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
import com.aguiabranca.app.core.ui.components.PillarChip
import com.aguiabranca.app.core.ui.components.formatDateShort
import com.aguiabranca.app.core.ui.local.LocalSession

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun GuidelinesScreen(
    onBack: () -> Unit,
    onAdmin: () -> Unit,
    onEdit: (String) -> Unit,
    vm: GuidelinesViewModel = hiltViewModel()
) {
    val session = LocalSession.current
    val items by vm.guidelines.collectAsState()
    var deleteId by remember { mutableStateOf<String?>(null) }

    Scaffold(
        topBar = {
            CenterAlignedTopAppBar(
                title = { Text("Orientações estratégicas") },
                navigationIcon = {
                    TextButton(onClick = onBack) { Text("Voltar") }
                }
            )
        },
        floatingActionButton = {
            if (session?.role == Role.LIDER) {
                ExtendedFloatingActionButton(
                    onClick = onAdmin,
                    icon = { Icon(Icons.Outlined.Add, contentDescription = null) },
                    text = { Text("Nova") }
                )
            }
        }
    ) { padding ->
        if (items.isEmpty()) {
            Box(modifier = Modifier.fillMaxSize().padding(padding), contentAlignment = Alignment.Center) {
                Text("Nenhuma orientação cadastrada", color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        } else {
            LazyColumn(modifier = Modifier.fillMaxSize().padding(padding), contentPadding = androidx.compose.foundation.layout.PaddingValues(16.dp)) {
                items(items, key = { it.id }) { g ->
                    Surface(
                        modifier = Modifier.fillMaxWidth().padding(vertical = 6.dp),
                        shape = RoundedCornerShape(14.dp),
                        color = MaterialTheme.colorScheme.surface
                    ) {
                        Column(modifier = Modifier.padding(16.dp)) {
                            Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                                Text(g.title, fontWeight = FontWeight.SemiBold, fontSize = 16.sp, modifier = Modifier.weight(1f))
                                if (session?.role == Role.LIDER) {
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
    }

    if (deleteId != null) {
        AlertDialog(
            onDismissRequest = { deleteId = null },
            title = { Text("Excluir orientação?") },
            text = { Text("Esta ação é permanente. Ideias vinculadas mostrarão \"Orientação removida\".") },
            confirmButton = {
                TextButton(onClick = {
                    val id = deleteId!!
                    vm.delete(id) { deleteId = null }
                }) { Text("Excluir") }
            },
            dismissButton = { TextButton(onClick = { deleteId = null }) { Text("Cancelar") } }
        )
    }
}
