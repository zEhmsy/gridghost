/*
 * Copyright 2026 sitecVendor. All Rights Reserved.
 */

import com.tridium.gradle.plugins.bajadoc.task.Bajadoc
import com.tridium.gradle.plugins.module.util.ModulePart.RuntimeProfile.*

plugins {
  id("com.tridium.niagara-module")
  id("com.tridium.niagara-signing")
  id("com.tridium.bajadoc")
  id("com.tridium.niagara-jacoco")
  id("com.tridium.niagara-annotation-processors")
  id("com.tridium.convention.niagara-home-repositories")
}

description = "GridGhost manifest importer Workbench manager"

moduleManifest {
  moduleName.set("gridGhostImporter")
  runtimeProfile.set(wb)
}

dependencies {
  nre(":nre")

  api(":baja")
  api(":bajaui-wb")
  api(":gx-rt")
  api(":workbench-wb")
  api(project(":gridGhostImporter-rt"))
}

tasks.named<Bajadoc>("bajadoc") {
  includePackage("com.sitecVendor.gridGhostImporter.wb")
}

tasks.named<Jar>("jar") {
  from("src") {
    include("img/**")
    include("resources/**")
  }
}
