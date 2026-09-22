package com.aguiabranca.app.navigation

import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.navigation.NavController
import androidx.navigation.NavGraph.Companion.findStartDestination
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.toRoute
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.ui.components.NavTab
import com.aguiabranca.app.core.ui.local.LocalSession
import com.aguiabranca.app.feature.auth.ui.LoginScreen
import com.aguiabranca.app.feature.dashboard.ui.DashboardScreen
import com.aguiabranca.app.feature.dashboard.ui.GuidelineDrillDownScreen
import com.aguiabranca.app.feature.guidelines.ui.GuidelinesAdminScreen
import com.aguiabranca.app.feature.guidelines.ui.GuidelinesScreen
import com.aguiabranca.app.feature.ideas.ui.CurationScreen
import com.aguiabranca.app.feature.ideas.ui.IdeaDetailScreen
import com.aguiabranca.app.feature.ideas.ui.MyIdeasScreen
import com.aguiabranca.app.feature.ideas.ui.NewIdeaScreen
import com.aguiabranca.app.feature.profile.ui.ProfileScreen
import com.aguiabranca.app.feature.projects.ui.NewProjectScreen
import com.aguiabranca.app.feature.projects.ui.ProjectDetailScreen
import com.aguiabranca.app.feature.projects.ui.ProjectEditScreen
import com.aguiabranca.app.feature.projects.ui.ProjectsListScreen

private fun NavController.navigateTab(route: Route) {
    navigate(route) {
        popUpTo(graph.findStartDestination().id) { saveState = true }
        launchSingleTop = true
        restoreState = true
    }
}

@Composable
private fun RoleGate(
    allowed: Set<Role>,
    onDenied: () -> Unit,
    content: @Composable () -> Unit
) {
    val session = LocalSession.current
    if (session == null || session.role !in allowed) {
        LaunchedEffect(session?.role) { onDenied() }
        return
    }
    content()
}

@Composable
fun AppNavHost(navController: NavHostController) {
    val session = LocalSession.current

    LaunchedEffect(session) {
        if (session == null) {
            val current = navController.currentDestination?.route
            val loginRoute = Route.Login::class.qualifiedName.orEmpty()
            if (current == null || !current.startsWith(loginRoute)) {
                navController.navigate(Route.Login) {
                    popUpTo(0) { inclusive = true }
                }
            }
        }
    }

    val startDestination: Route = when (session?.role) {
        Role.OPERADOR -> Route.MyIdeas
        Role.GESTOR -> Route.Curation
        Role.LIDER -> Route.Dashboard
        null -> Route.Login
    }

    val onTab: (NavTab) -> Unit = { tab ->
        val route: Route? = when (tab) {
            NavTab.IDEAS -> Route.MyIdeas
            NavTab.CURATION -> Route.Curation
            NavTab.PROJECTS -> Route.Projects
            NavTab.DASHBOARD -> Route.Dashboard
            NavTab.GUIDELINES -> Route.Guidelines
            NavTab.PROFILE -> Route.Profile
        }
        route?.let { navController.navigateTab(it) }
    }

    NavHost(navController = navController, startDestination = startDestination) {
        composable<Route.Login> {
            LoginScreen(
                onLoggedIn = { role ->
                    val home: Route = when (role) {
                        Role.OPERADOR -> Route.MyIdeas
                        Role.GESTOR -> Route.Curation
                        Role.LIDER -> Route.Dashboard
                    }
                    navController.navigate(home) {
                        popUpTo(0) { inclusive = true }
                    }
                }
            )
        }
        composable<Route.MyIdeas> {
            MyIdeasScreen(
                onOpenIdea = { id -> navController.navigate(Route.IdeaDetail(id)) },
                onNewIdea = { navController.navigate(Route.NewIdea) },
                onTab = onTab
            )
        }
        composable<Route.NewIdea> {
            NewIdeaScreen(
                onDone = { navController.popBackStack() },
                onCancel = { navController.popBackStack() }
            )
        }
        composable<Route.IdeaDetail> { entry ->
            val args = entry.toRoute<Route.IdeaDetail>()
            IdeaDetailScreen(
                ideaId = args.id,
                onBack = { navController.popBackStack() }
            )
        }
        composable<Route.Guidelines> {
            GuidelinesScreen(
                onAdmin = { navController.navigate(Route.GuidelinesAdmin) },
                onEdit = { id -> navController.navigate(Route.GuidelineEdit(id)) },
                onTab = onTab
            )
        }
        composable<Route.GuidelinesAdmin> {
            RoleGate(allowed = setOf(Role.LIDER), onDenied = { navController.popBackStack() }) {
                GuidelinesAdminScreen(
                    guidelineId = null,
                    onDone = { navController.popBackStack() }
                )
            }
        }
        composable<Route.GuidelineEdit> { entry ->
            val args = entry.toRoute<Route.GuidelineEdit>()
            RoleGate(allowed = setOf(Role.LIDER), onDenied = { navController.popBackStack() }) {
                GuidelinesAdminScreen(
                    guidelineId = args.id,
                    onDone = { navController.popBackStack() }
                )
            }
        }
        composable<Route.Profile> {
            ProfileScreen(
                onLoggedOut = {
                    navController.navigate(Route.Login) {
                        popUpTo(0) { inclusive = true }
                    }
                },
                onTab = onTab
            )
        }
        composable<Route.Curation> {
            RoleGate(allowed = setOf(Role.GESTOR), onDenied = { navController.popBackStack() }) {
                CurationScreen(
                    onOpenIdea = { id -> navController.navigate(Route.IdeaDetail(id)) },
                    onTab = onTab
                )
            }
        }
        composable<Route.Projects> {
            ProjectsListScreen(
                onOpenProject = { id -> navController.navigate(Route.ProjectDetail(id)) },
                onNewProject = { navController.navigate(Route.NewProject) },
                onTab = onTab
            )
        }
        composable<Route.ProjectDetail> { entry ->
            val args = entry.toRoute<Route.ProjectDetail>()
            ProjectDetailScreen(
                projectId = args.id,
                onBack = { navController.popBackStack() },
                onEdit = { navController.navigate(Route.EditProject(args.id)) }
            )
        }
        composable<Route.NewProject> {
            RoleGate(allowed = setOf(Role.GESTOR), onDenied = { navController.popBackStack() }) {
                NewProjectScreen(
                    onDone = { id -> navController.navigate(Route.ProjectDetail(id)) { popUpTo(Route.Projects) } },
                    onCancel = { navController.popBackStack() }
                )
            }
        }
        composable<Route.EditProject> { entry ->
            val args = entry.toRoute<Route.EditProject>()
            RoleGate(allowed = setOf(Role.GESTOR), onDenied = { navController.popBackStack() }) {
                ProjectEditScreen(
                    projectId = args.id,
                    onDone = { navController.popBackStack() },
                    onCancel = { navController.popBackStack() }
                )
            }
        }
        composable<Route.Dashboard> {
            RoleGate(allowed = setOf(Role.LIDER), onDenied = { navController.popBackStack() }) {
                DashboardScreen(
                    onOpenGuideline = { id -> navController.navigate(Route.GuidelineDrillDown(id)) },
                    onOpenProject = { id -> navController.navigate(Route.ProjectDetail(id)) },
                    onTab = onTab
                )
            }
        }
        composable<Route.GuidelineDrillDown> { entry ->
            val args = entry.toRoute<Route.GuidelineDrillDown>()
            RoleGate(allowed = setOf(Role.LIDER), onDenied = { navController.popBackStack() }) {
                GuidelineDrillDownScreen(
                    guidelineId = args.id,
                    onBack = { navController.popBackStack() },
                    onOpenProject = { id -> navController.navigate(Route.ProjectDetail(id)) }
                )
            }
        }
    }
}
