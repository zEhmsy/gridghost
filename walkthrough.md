# GridGhost Generators

GridGhost includes several value generators to automatically animate points over time without external stimuli. They can be selected from the Points Monitor View. 

To edit generator parameters, click the `⚙️` (settings) button next to the active generator type for any point. Note that **values can only be edited while the device is stopped**. 

### Available Generators & Parameters:
- **Static:** No parameters needed. The point remains at its manually requested value.
- **Random:** Uses `Min`, `Max`, and `Period (s)` to determine the frequency of randomized fluctuations.
- **Sine:** Uses `Min`, `Max`, and `Period (s)` to shape a repeating sine wave.
- **Ramp:** Uses `Min`, `Max`, and `Period (s)` to determine the timing loops, and uses `Step` to define the incremental value to add per tick.

All settings are persisted inside the explicit device instance configuration. Modifying them will not mutate the master template definitions unless saved manually inside the dedicated Template Editor.
