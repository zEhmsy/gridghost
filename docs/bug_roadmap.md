# GridGhost Bugfix Roadmap

## 1. Template save crash
- **Severity**: Critical
- **Root Cause Hypothesis**: JSON serialization errors or file IO locks causing unhandled exceptions during template save operations.
- **Proposed Fix**: Implement atomic save (write to temp file, then replace). Add try-catch around IO operations showing a graceful error dialog instead of crashing.
- **Tests**: Save valid/invalid templates. Verify file IO error handling.
- **Acceptance Criteria**: Saving a template never crashes the app. Errors are shown in UI.

## 2. UI: "Set" changes require Save + warning on exit
- **Severity**: High
- **Root Cause Hypothesis**: No dirty-state tracking exists for Points/Template Editor views, allowing silent loss of unsaved changes.
- **Proposed Fix**: Add an `IsDirty` flag to ViewModels. Intercept navigation and window close events to prompt the user to Save/Discard/Cancel.
- **Tests**: Modify view, attempt navigation, verify prompt.
- **Acceptance Criteria**: Unsaved changes trigger a prompt; data is safely saved or discarded based on user choice.

## 3. Device ID on same port (collisions)
- **Severity**: High
- **Root Cause Hypothesis**: The underlying Modbus TCP implementation might bind to the port exclusively without properly routing by Unit ID, or multiple instances predictably clash over socket resources.
- **Proposed Fix**: We will enforce one device per port for now. Creating or starting a second device on the same active port will show a clear error dialogue rejecting it. 
- **Tests**: Try to start two devices on the same port with different Unit IDs. Verify a clear error message enforcing one per port.
- **Acceptance Criteria**: No crashes binding to in-use ports. User gets a clear warning.

## 4. Devices with same names cause bugs
- **Severity**: Medium
- **Root Cause Hypothesis**: Application logic depends on the display `Name` as a unique identifier (e.g. `DeviceManager` lookup maps, or UI element collections).
- **Proposed Fix**: Ensure all internal runtime maps and bindings use strictly internal GUIDs (`Id`) for internal lookups.
- **Tests**: Create two devices with the same name. Interacting with one does not affect the other.
- **Acceptance Criteria**: Internal structures use robust keys avoiding naming collisions.

## 5. Changing point data type doesn't apply
- **Severity**: Medium
- **Root Cause Hypothesis**: Changing the `Type` combo box in the Editor UI doesn't cascade down to re-configure the underlying `ModbusDataStore` runtime, maintaining the previous type encoding (typically bool or int16 default).
- **Proposed Fix**: Update logic to only allow changing data type when the device is stopped. Upon change, sync the backing model so it generates and reads correctly over Modbus.
- **Tests**: Change type in UI, start device, read point. Ensure length and format match new type.
- **Acceptance Criteria**: UI data types reliably match Modbus simulated register data mapping.

## 6. Holding override not working
- **Severity**: Medium
- **Root Cause Hypothesis**: The Modbus server generator continuously overrides the mapped values of points, overwriting any incoming Modbus Write requests instantly.
- **Proposed Fix**: Implement external write check for the Generator loop. If `ExternalWriteOverrideMode` is triggered (Hold/Force Static), the generator pauses injection to that point map.
- **Tests**: External write to a holding register is not immediately overwritten by the wave generator if configured to hold/override.
- **Acceptance Criteria**: Real Modbus writes are respected and visually confirmed without flicker.

## 7. After creating instance from template, auto-navigate to Points view
- **Severity**: Low (UX)
- **Root Cause Hypothesis**: Logic currently lacks a navigation hook after instance instantiation, defaulting to no op or template screen.
- **Proposed Fix**: Inject a navigation command upon successful creation, routing to the Points view of that newly instanced device.
- **Tests**: End-to-end device creation seamlessly opens the Points tab for the created device.
- **Acceptance Criteria**: Smooth transition from template browser to active point editing.

## 8. UI: resize columns horizontally
- **Severity**: Low (UX)
- **Root Cause Hypothesis**: `DataGrid` columns in Avalonia XAML are currently configured with fixed or unassigned resizability flags.
- **Proposed Fix**: Add `CanUserResize="True"` attributes explicitly on `DataGridTextColumn`s within the application grids.
- **Tests**: Manual UI verification: user can hover and drag column headers in standard views.
- **Acceptance Criteria**: Columns are horizontally interactive.
