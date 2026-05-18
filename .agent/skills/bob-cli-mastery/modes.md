# Bob CLI Modes Documentation

Modes are specialized personas that tailor Bob's behavior for your specific tasks. Each mode offers different capabilities and access levels to help you accomplish particular goals more efficiently.

### Why use different modes?
* **Task specialization**: Get precisely the type of assistance you need for your current task
* **Safety controls**: Prevent unintended file modifications when focusing on planning or learning
* **Focused interactions**: Receive responses optimized for your current activity
* **Workflow optimization**: Transition seamlessly between planning, implementing, debugging, and learning

### Switching between modes
You have 4 options to switch modes:
* **Drop-down menu:** Click the selector to the left of the chat input
* **Slash command:** Type `/plan`, `/ask`, `/code`, `/advanced`, or `/orchestrator` at the beginning of your message.
* **Toggle command/Keyboard shortcut:** `Ctrl + .` (Windows/Linux) or `⌘ + .` (macOS).
* **Accept suggestions:** Click on mode switch suggestions that Bob offers when appropriate

### Built-in modes

## Code mode
| Aspect                | Details |
| --------------------- | ------- |
| **Name**              | `💻 Code` |
| **Short description** | Write, modify, and refactor code. |
| **Tool access**       | `read`, `edit`, `command` |
| **Use case**          | General purpose coding tasks, optimized for cost efficiency. |

## Ask mode
| Aspect                | Details |
| --------------------- | ------- |
| **Name**              | `❓ Ask` |
| **Short description** | Get answers and explanations. |
| **Tool access**       | `read`, `browser`, `mcp` |
| **Use case**          | Conversational questions and information about your code. |

## Plan mode
| Aspect                | Details |
| --------------------- | ------- |
| **Name**              | `📝 Plan` |
| **Short description** | Plan and design before implementation. |
| **Tool access**       | `read`, `edit` - markdown only, `browser`, `mcp` |
| **Use case**          | High-level planning and technical leadership. Big picture thinking! |

## Advanced mode
| Aspect                | Details |
| --------------------- | ------- |
| **Name**              | `🛠️ Advanced` |
| **Short description** | Advanced version of Code mode, with more tools. |
| **Tool access**       | All tool groups: `read`, `edit`, `command`, `mcp` |
| **Use case**          | Advanced coding tasks, requiring access to MCP tools. Power users! |

## Orchestrator mode
| Aspect                | Details |
| --------------------- | ------- |
| **Name**              | `🔀 Orchestrator` |
| **Short description** | Coordinate tasks across multiple modes |
| **Tool access**       | None |
| **Use case**          | Complex projects requiring coordination across different specialties, multi-domain workflows, and task management |
