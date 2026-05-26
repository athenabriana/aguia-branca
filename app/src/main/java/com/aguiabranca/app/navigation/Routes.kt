package com.aguiabranca.app.navigation

import kotlinx.serialization.Serializable

sealed interface Route {
    @Serializable data object Login : Route
    @Serializable data object MyIdeas : Route
    @Serializable data object NewIdea : Route
    @Serializable data class IdeaDetail(val id: String) : Route
    @Serializable data object Guidelines : Route
    @Serializable data object GuidelinesAdmin : Route
    @Serializable data class GuidelineEdit(val id: String) : Route
    @Serializable data object Profile : Route
    @Serializable data object Curation : Route
    @Serializable data object Projects : Route
    @Serializable data class ProjectDetail(val id: String) : Route
    @Serializable data object NewProject : Route
    @Serializable data class EditProject(val id: String) : Route
    @Serializable data object Dashboard : Route
    @Serializable data class GuidelineDrillDown(val id: String) : Route
}
