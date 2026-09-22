package com.aguiabranca.app.core.ui.components

import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Description
import androidx.compose.material.icons.outlined.Insights
import androidx.compose.material.icons.outlined.Lightbulb
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material.icons.outlined.Rule
import androidx.compose.material.icons.outlined.Work
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import com.aguiabranca.app.core.domain.model.Role

enum class NavTab { IDEAS, CURATION, PROJECTS, DASHBOARD, GUIDELINES, PROFILE }

private data class NavItem(val tab: NavTab, val label: String, val icon: ImageVector)

private fun itemsFor(role: Role): List<NavItem> = when (role) {
    Role.OPERADOR -> listOf(
        NavItem(NavTab.IDEAS, "Ideias", Icons.Outlined.Lightbulb),
        NavItem(NavTab.GUIDELINES, "Orientações", Icons.Outlined.Description),
        NavItem(NavTab.PROFILE, "Perfil", Icons.Outlined.Person)
    )
    Role.GESTOR -> listOf(
        NavItem(NavTab.CURATION, "Curadoria", Icons.Outlined.Rule),
        NavItem(NavTab.PROJECTS, "Projetos", Icons.Outlined.Work),
        NavItem(NavTab.GUIDELINES, "Orientações", Icons.Outlined.Description),
        NavItem(NavTab.PROFILE, "Perfil", Icons.Outlined.Person)
    )
    Role.LIDER -> listOf(
        NavItem(NavTab.DASHBOARD, "Dashboard", Icons.Outlined.Insights),
        NavItem(NavTab.PROJECTS, "Projetos", Icons.Outlined.Work),
        NavItem(NavTab.GUIDELINES, "Orientações", Icons.Outlined.Description),
        NavItem(NavTab.PROFILE, "Perfil", Icons.Outlined.Person)
    )
}

@Composable
fun RoleScaffold(
    role: Role,
    currentTab: NavTab,
    onTabClick: (NavTab) -> Unit,
    topBar: @Composable () -> Unit = {},
    content: @Composable (PaddingValues) -> Unit
) {
    val items = itemsFor(role)
    Scaffold(
        topBar = topBar,
        bottomBar = {
            NavigationBar {
                items.forEach { item ->
                    NavigationBarItem(
                        selected = currentTab == item.tab,
                        onClick = { onTabClick(item.tab) },
                        icon = { Icon(item.icon, contentDescription = item.label) },
                        label = { Text(item.label) }
                    )
                }
            }
        },
        content = content
    )
}
