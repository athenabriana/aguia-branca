package com.aguiabranca.app.core.di

import com.aguiabranca.app.core.domain.GuidelinesRepository
import com.aguiabranca.app.core.domain.IdeasRepository
import com.aguiabranca.app.core.domain.ProjectsRepository
import com.aguiabranca.app.core.domain.ReportsRepository
import com.aguiabranca.app.core.domain.UsersRepository
import com.aguiabranca.app.feature.dashboard.data.RemoteReportsRepository
import com.aguiabranca.app.feature.auth.data.RemoteUsersRepository
import com.aguiabranca.app.feature.guidelines.data.RemoteGuidelinesRepository
import com.aguiabranca.app.feature.ideas.data.RemoteIdeasRepository
import com.aguiabranca.app.feature.projects.data.RemoteProjectsRepository
import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class RepositoryModule {
    @Binds @Singleton abstract fun bindUsers(impl: RemoteUsersRepository): UsersRepository
    @Binds @Singleton abstract fun bindGuidelines(impl: RemoteGuidelinesRepository): GuidelinesRepository
    @Binds @Singleton abstract fun bindIdeas(impl: RemoteIdeasRepository): IdeasRepository
    @Binds @Singleton abstract fun bindReports(impl: RemoteReportsRepository): ReportsRepository
    @Binds @Singleton abstract fun bindProjects(impl: RemoteProjectsRepository): ProjectsRepository
}
