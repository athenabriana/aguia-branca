package com.aguiabranca.app.navigation

import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Modifier
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.toRoute
import com.aguiabranca.app.core.ui.local.LocalSession
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.feature.auth.ui.LoginScreen
import com.aguiabranca.app.feature.dashboard.ui.DashboardScreen
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
import com.aguiabranca.app.feature.dashboard.ui.GuidelineDrillDownScreen

@Composable
fun AppNavHost(navController: NavHostController) {
    val session = LocalSession.current

    LaunchedEffect(session) {
        val current = navController.currentDestination?.route
        if (session == null && current?.startsWith(Route.Login::class.qualifiedName ?: "") == false) {
            navController.navigate(Route.Login) {
                popUpTo(0) { inclusive = true }
            }
        }
    }

    val startDestination: Route = when (session?.role) {
        Role.OPERADOR -> Route.MyIdeas
        Role.GESTOR -> Route.Curation
        Role.LIDER -> Route.Dashboard
        null -> Route.Login
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
                onGuidelines = { navController.navigate(Route.Guidelines) },
                onProfile = { navController.navigate(Route.Profile) }
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
                onBack = { navController.popBackStack() },
                onAdmin = { navController.navigate(Route.GuidelinesAdmin) },
                onEdit = { id -> navController.navigate(Route.GuidelineEdit(id)) }
            )
        }
        composable<Route.GuidelinesAdmin> {
            GuidelinesAdminScreen(
                guidelineId = null,
                onDone = { navController.popBackStack() }
            )
        }
        composable<Route.GuidelineEdit> { entry ->
            val args = entry.toRoute<Route.GuidelineEdit>()
            GuidelinesAdminScreen(
                guidelineId = args.id,
                onDone = { navController.popBackStack() }
            )
        }
        composable<Route.Profile> {
            ProfileScreen(
                onBack = { navController.popBackStack() }
            )
        }
        composable<Route.Curation> {
            CurationScreen(
                onOpenIdea = { id -> navController.navigate(Route.IdeaDetail(id)) },
                onProjects = { navController.navigate(Route.Projects) },
                onGuidelines = { navController.navigate(Route.Guidelines) },
                onProfile = { navController.navigate(Route.Profile) }
            )
        }
        composable<Route.Projects> {
            ProjectsListScreen(
                onOpenProject = { id -> navController.navigate(Route.ProjectDetail(id)) },
                onNewProject = { navController.navigate(Route.NewProject) },
                onCuration = { navController.navigate(Route.Curation) },
                onGuidelines = { navController.navigate(Route.Guidelines) },
                onProfile = { navController.navigate(Route.Profile) }
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
            NewProjectScreen(
                onDone = { id -> navController.navigate(Route.ProjectDetail(id)) { popUpTo(Route.Projects) } },
                onCancel = { navController.popBackStack() }
            )
        }
        composable<Route.EditProject> { entry ->
            val args = entry.toRoute<Route.EditProject>()
            ProjectEditScreen(
                projectId = args.id,
                onDone = { navController.popBackStack() },
                onCancel = { navController.popBackStack() }
            )
        }
        composable<Route.Dashboard> {
            DashboardScreen(
                onOpenGuideline = { id -> navController.navigate(Route.GuidelineDrillDown(id)) },
                onOpenProject = { id -> navController.navigate(Route.ProjectDetail(id)) },
                onProjects = { navController.navigate(Route.Projects) },
                onGuidelines = { navController.navigate(Route.Guidelines) },
                onProfile = { navController.navigate(Route.Profile) }
            )
        }
        composable<Route.GuidelineDrillDown> { entry ->
            val args = entry.toRoute<Route.GuidelineDrillDown>()
            GuidelineDrillDownScreen(
                guidelineId = args.id,
                onBack = { navController.popBackStack() },
                onOpenProject = { id -> navController.navigate(Route.ProjectDetail(id)) }
            )
        }
    }
}
