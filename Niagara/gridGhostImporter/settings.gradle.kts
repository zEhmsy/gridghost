/*
 * Copyright 2026 sitecVendor. All Rights Reserved.
 */

import com.tridium.gradle.plugins.settings.MultiProjectExtension
import com.tridium.gradle.plugins.settings.LocalSettingsExtension

pluginManagement {
  val niagaraHome: Provider<String> = providers.gradleProperty("niagara_home").orElse(
          providers.systemProperty("niagara_home").orElse(
                  providers.environmentVariable("NIAGARA_HOME").orElse(
                          providers.environmentVariable("niagara_home")
                  )
          )
  )

  val gradlePluginHome: String = providers.gradleProperty("gradlePluginHome").orElse(
          providers.environmentVariable("GRADLE_PLUGIN_HOME").orElse(
                  niagaraHome.map { "$it/etc/m2/repository" }
          )
  ).orNull ?: throw InvalidUserDataException("Cannot derive value of 'gradlePluginHome'. Set niagara_home in gradle.properties or NIAGARA_HOME env var.")

  val gradlePluginRepoUrl = "file:///${gradlePluginHome.replace('\\', '/')}"
  val gradlePluginVersion = "7.6.22"
  val settingsPluginVersion = "7.6.3"

  repositories {
    maven(url = gradlePluginRepoUrl)
    gradlePluginPortal()
  }

  plugins {
    id("com.tridium.settings.multi-project") version settingsPluginVersion
    id("com.tridium.settings.local-settings-convention") version settingsPluginVersion

    id("com.tridium.niagara") version gradlePluginVersion
    id("com.tridium.vendor") version gradlePluginVersion
    id("com.tridium.niagara-module") version gradlePluginVersion
    id("com.tridium.niagara-signing") version gradlePluginVersion
    id("com.tridium.bajadoc") version gradlePluginVersion
    id("com.tridium.niagara-jacoco") version gradlePluginVersion
    id("com.tridium.niagara-annotation-processors") version gradlePluginVersion
    id("com.tridium.convention.niagara-home-repositories") version gradlePluginVersion
  }
}

plugins {
  id("com.tridium.settings.multi-project")
  id("com.tridium.settings.local-settings-convention")
}

configure<LocalSettingsExtension> {
  loadLocalSettings()
}

configure<MultiProjectExtension> {
  findProjects()
}

rootProject.name = "gridGhostImporter"
include("gridGhostImporter-rt")
include("gridGhostImporter-wb")
