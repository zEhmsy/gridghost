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

description = "GridGhost manifest importer runtime service"

moduleManifest {
  moduleName.set("gridGhostImporter")
  runtimeProfile.set(rt)
}

dependencies {
  nre(":nre")

  api(":baja")
  api(":control-rt")
  api(":driver-rt")
  api(":basicDriver-rt")
  api(":gx-rt")
  api(":modbusCore-rt")
  api(":modbusTcp-rt")
  api(":alarm-rt")
}

tasks.named<Bajadoc>("bajadoc") {
  includePackage("com.sitecVendor.gridGhostImporter")
}

tasks.named<Jar>("jar") {
  from("src") {
    include("img/**")
    include("resources/**")
  }
}
