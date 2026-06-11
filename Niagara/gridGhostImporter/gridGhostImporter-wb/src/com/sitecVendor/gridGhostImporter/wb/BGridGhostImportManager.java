/*
 * Copyright 2026 sitecVendor. All Rights Reserved.
 */
package com.sitecVendor.gridGhostImporter.wb;

import com.sitecVendor.gridGhostImporter.BGridGhostImportService;
import javax.baja.gx.BFont;
import javax.baja.gx.BInsets;
import javax.baja.nre.annotations.AgentOn;
import javax.baja.nre.annotations.NiagaraType;
import javax.baja.sys.BObject;
import javax.baja.sys.Context;
import javax.baja.sys.Sys;
import javax.baja.sys.Type;
import javax.baja.ui.BButton;
import javax.baja.ui.BLabel;
import javax.baja.ui.Command;
import javax.baja.ui.CommandArtifact;
import javax.baja.ui.pane.BBorderPane;
import javax.baja.ui.pane.BEdgePane;
import javax.baja.ui.pane.BGridPane;
import javax.baja.workbench.view.BWbComponentView;
import javax.swing.JFileChooser;
import javax.swing.JOptionPane;
import javax.swing.filechooser.FileNameExtensionFilter;

import java.io.File;
import java.nio.file.Files;
import java.util.Base64;

@NiagaraType(
    agent = @AgentOn(
        types = "gridGhostImporter:GridGhostImportService",
        defaultAgent = AgentOn.Preference.PREFERRED
    )
)
public final class BGridGhostImportManager extends BWbComponentView {

  private static final BFont TITLE = BFont.make("SansSerif", 14, BFont.BOLD);
  private BGridGhostImportService service;

  public BGridGhostImportManager() {
    setContent(new BLabel("Load a GridGhostImportService component."));
  }

  @Override
  protected void doLoadValue(BObject value, Context cx) {
    service = (BGridGhostImportService) value;
    rebuild();
  }

  private void rebuild() {
    if (service == null) {
      setContent(new BLabel("No GridGhostImportService loaded."));
      return;
    }

    BGridPane details = new BGridPane(2);
    details.setColumnGap(14);
    addRow(details, "Manifest", String.valueOf(service.getManifestOrd()));
    addRow(details, "Station root", String.valueOf(service.getStationRoot()));
    addRow(details, "Network", service.getNetworkSlotName());
    addRow(details, "Status", service.getLastImportStatus());
    addRow(details, "Last error", service.getLastError());
    addRow(details, "Last import", service.getLastImportedAt());
    addRow(details, "Created", String.valueOf(service.getCreatedCount()));
    addRow(details, "Updated", String.valueOf(service.getUpdatedCount()));
    addRow(details, "Skipped", String.valueOf(service.getSkippedCount()));

    BEdgePane actions = new BEdgePane();
    actions.setLeft(new BButton(new ValidateCommand(this)));
    actions.setCenter(new BButton(new ImportCommand(this)));
    actions.setRight(new BButton(new RefreshCommand(this)));

    BEdgePane body = new BEdgePane();
    body.setTop(new BLabel("GridGhost Niagara Importer", TITLE));
    body.setCenter(details);
    body.setBottom(actions);

    BBorderPane frame = new BBorderPane(body);
    frame.setPadding(BInsets.make(16));
    setContent(frame);
    relayout();
    repaint();
  }

  private static void addRow(BGridPane grid, String label, String value) {
    grid.add(null, new BLabel(label));
    grid.add(null, new BLabel(value == null ? "" : value));
  }

  private static final class ValidateCommand extends Command {
    private final BGridGhostImportManager view;

    ValidateCommand(BGridGhostImportManager view) {
      super(view, "");
      this.view = view;
    }

    @Override public String getLabel() { return "Validate"; }

    @Override public CommandArtifact doInvoke() {
      if (view.service != null) view.service.validateManifest();
      view.rebuild();
      return null;
    }
  }

  private static final class ImportCommand extends Command {
    private final BGridGhostImportManager view;

    ImportCommand(BGridGhostImportManager view) {
      super(view, "");
      this.view = view;
    }

    @Override public String getLabel() { return "Import Now"; }

    @Override public CommandArtifact doInvoke() {
      if (view.service == null) return null;

      JFileChooser chooser = new JFileChooser();
      chooser.setDialogTitle("Select GridGhost Niagara manifest");
      chooser.setFileFilter(new FileNameExtensionFilter("JSON files", "json"));

      int result = chooser.showOpenDialog(null);
      if (result != JFileChooser.APPROVE_OPTION) return null;

      File selected = chooser.getSelectedFile();
      try {
        byte[] bytes = Files.readAllBytes(selected.toPath());
        String stationName = safeStationFileName(selected.getName());
        String encoded = Base64.getEncoder().encodeToString(bytes);

        view.service.setPendingUploadName(stationName);
        view.service.setPendingUploadB64(encoded);
        view.service.uploadManifest();
        view.service.importNow();

        JOptionPane.showMessageDialog(
            null,
            "Manifest uploaded to station shared folder and import started:\n"
                + BGridGhostImportService.sharedFileOrd(stationName),
            "GridGhost Import",
            JOptionPane.INFORMATION_MESSAGE
        );
      } catch (Exception e) {
        JOptionPane.showMessageDialog(
            null,
            "Unable to import manifest:\n" + e.getMessage(),
            "GridGhost Import",
            JOptionPane.ERROR_MESSAGE
        );
      }
      view.rebuild();
      return null;
    }

    private static String safeStationFileName(String name) {
      String safe = name == null ? "" : name.trim().replace('\\', '/');
      int slash = safe.lastIndexOf('/');
      if (slash >= 0) safe = safe.substring(slash + 1);
      safe = safe.replaceAll("[^a-zA-Z0-9_.-]", "_");
      if (safe.length() == 0 || safe.indexOf("..") >= 0) {
        safe = "gridghost-niagara-manifest.json";
      }
      if (!safe.toLowerCase().endsWith(".json")) safe = safe + ".json";
      return safe;
    }
  }

  private static final class RefreshCommand extends Command {
    private final BGridGhostImportManager view;

    RefreshCommand(BGridGhostImportManager view) {
      super(view, "");
      this.view = view;
    }

    @Override public String getLabel() { return "Refresh"; }

    @Override public CommandArtifact doInvoke() {
      view.rebuild();
      return null;
    }
  }

  public static final Type TYPE = Sys.loadType(BGridGhostImportManager.class);
  @Override public Type getType() { return TYPE; }
}
