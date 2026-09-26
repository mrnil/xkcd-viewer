# Refactoring Patterns

When and how to split a growing Logi Actions SDK plugin. None of this applies yet: use it once `XkcdPlugin.cs` or a service shows the warning signs below.

## God Class Refactoring

### Problem

A single main plugin class accumulates connection lifecycle, command registration, state queries, action delegation, and event routing all at once — a God Class violating the Single Responsibility Principle.

### Solution

Split into focused classes, each with a single responsibility:

#### 1. A Connection/Session Manager

**Responsibility:** External connection lifecycle

Handles:

- Reading configuration
- Waiting for availability (port checks, auth, etc.)
- Connecting to the external integration
- Disconnecting and cleanup

#### 2. A Command Coordinator

**Responsibility:** Command Registration & Event Notification

Handles:

- Command registration
- Event notification to commands
- Delegation to a command registry

#### 3. A Facade

**Responsibility:** State & Actions Interface

Handles:

- State queries
- Query methods
- Action methods
- Connection validation

#### 4. The Main Plugin Class

**Responsibility:** Orchestration & Event Routing

Handles:

- Plugin lifecycle orchestration
- Event routing to the coordinator
- Public API delegation to the facade
- Resource management

### Benefits

- **Single Responsibility Principle** - Each class has one clear purpose
- **Easier to Test** - Mock only what you need
- **Easier to Understand** - Focused, cohesive classes
- **Easier to Change** - Changes isolated to specific classes
- **Better Reusability** - Components can be used independently

## Command Registry Pattern

### Problem

Manual command registration/notification scattered through the main plugin class:

- Many manual notification calls
- Easy to forget wiring up a new command
- No compile-time safety
- Scattered notification logic

### Solution

Interface-based self-registration with a command registry:

```csharp
// Interface hierarchy
public interface IPluginCommand
{
    void OnConnected();
    void OnDisconnected();
}

public interface IItemAwareCommand : IPluginCommand
{
    void OnItemChanged(String itemName);
}

// Commands self-register
public class ItemsDynamicFolder : PluginDynamicFolder, IPluginCommand, IItemAwareCommand
{
    public ItemsDynamicFolder()
    {
        Instance = this;
        XkcdPlugin.Instance?.RegisterCommand(this);
    }

    public void OnConnected() { }
    public void OnDisconnected() { }
    public void OnItemChanged(String itemName) { }
}

// Registry notifies via interfaces
public class CommandRegistry
{
    public void NotifyItemChanged(String itemName)
    {
        foreach (var command in this._commands.OfType<IItemAwareCommand>())
        {
            command.OnItemChanged(itemName);
        }
    }
}
```

### Benefits

- **Self-Documenting** - Commands register themselves
- **Compile-Time Safety** - Interfaces enforce implementation
- **No Maintenance** - Adding new commands requires no changes to the registry
- **Centralized** - All notification logic in one place
- **Testable** - Registry can be mocked independently

Prefer per-command exception isolation in the dispatch loop (`try`/`catch` around each command's callback) so one throwing command doesn't block notification of the rest.

## Facade Pattern

### Problem

Direct access to a connection manager from the main plugin class:

- Repeated null checking
- Repeated connection validation
- Complex delegation chains
- Scattered error handling

### Solution

A facade class provides a simplified interface:

```csharp
public class ComicFacade
{
    private readonly IXkcdClient _client;
    private readonly ComicCache _cache;
    private readonly IPluginLog _log;

    public XkcdComic Current => this._cache.Current;

    public async Task ShowComicAsync(Int32 number)
    {
        if (number < 1 || number > this._cache.LatestNumber)
        {
            this._log.Warning($"Cannot show comic {number} - out of range");
            return;
        }

        this._cache.Current = this._cache.Get(number) ?? await this._client.GetComicAsync(number);
    }
}
```

### Benefits

- **Simplified Interface** - Hide complexity
- **Consistent Validation** - Connection checks in one place
- **Better Error Handling** - Centralized logging
- **Easier Testing** - Mock the facade instead of the manager

## Refactoring Principles Applied

### 1. Single Responsibility Principle (SRP)

Each class should have one reason to change. Applied to: connection lifecycle, command coordination, external-service interface, orchestration — as separate classes.

### 2. Open/Closed Principle (OCP)

Open for extension, closed for modification. Applied to: adding new commands without modifying the registry; adding new event types without changing the base interface.

### 3. Dependency Inversion Principle (DIP)

Depend on abstractions, not concretions. Applied to: commands depending on notification interfaces; services depending on an interface (`IXkcdClient`) rather than a concrete client.

### 4. Don't Repeat Yourself (DRY)

Eliminate duplication. Applied to: centralizing connection logic in one manager class, centralizing notification logic in one registry.

### 5. Separation of Concerns

Different concerns should be in different classes: connection concerns, command concerns, external-service concerns, orchestration concerns — each isolated.

## When to Refactor

### Warning Signs

- Class exceeds 300-400 lines
- Class has 5+ distinct responsibilities
- Hard to write tests without mocking everything
- Developers avoid changing it (fear of breaking things)
- Pull requests touching it are large and risky
- New features always require editing this class

### Refactoring Process

1. **Identify responsibilities** - List what the class does
2. **Group related concerns** - Connection, Commands, Actions, etc.
3. **Extract focused classes** - One responsibility per class
4. **Delegate from main class** - Thin orchestration layer
5. **Test thoroughly** - Ensure no regressions

### Anti-Patterns to Avoid

- Creating too many tiny classes (over-engineering)
- Splitting arbitrarily (not by responsibility)
- Leaving main class still doing too much
- Not updating tests

### Best Practices

- Split by responsibility (SRP)
- Keep classes focused and cohesive
- Use dependency injection
- Test each class independently
- Document the architecture
