# C# style and cleanup

Read when writing, editing or reviewing C# code, including tests. These are required rules for touched code,
not permission for unrelated cleanup. Read only the affected code before deciding its grouping.

Namespaces must match file locations and project root namespaces. Document public APIs with XML comments.
Use precise variable names that distinguish IDs, entities, names and serialized values.
EditorConfig overrides overlapping solution settings; the grouping priorities below take precedence
where formatter settings cannot express them.

### C# readability and cleanup

These rules replace the previous C# operation-grouping and blank-line rules. Apply the priorities
below in order; once a group is formed, lower priorities cannot split it, extend it, or reuse its
statements. Apply them while writing or touching code, including property accessors.

#### Terminology and scope

- An operation is a complete statement. A single-line operation occupies one physical source line (editor soft wrapping
  does not count). Do not put multiple independent statements on one line.
- Control-flow statements (`for`, `foreach`, `while`, `do-while`, `switch`, `if/else`, and
  `try/catch/finally`) always follow the multiline rules, regardless of physical line count.
  Their bodies must not share the header's line. Other statements are multiline when their source
  spans two or more physical lines. A switch expression is part of its containing operation.
- A multiline lambda subscription or object initializer is one operation. Format nested executable
  blocks independently. Keep `else`, `catch`, `finally`, and the trailing `while` attached to their
  complete construct. Apply the same structural treatment to `using (...)`, `await using (...)`,
  `lock`, `fixed`, `checked`, `unchecked`, `unsafe`, standalone blocks, and local function bodies.
- Groups contain consecutive operations with no blank lines between them and exactly one blank line
  between neighboring groups. Never put a blank line at the beginning or end of a block. Keep blank
  lines free of trailing whitespace. Comments stay attached to the statements they describe; they
  are not operations. Preprocessor directive boundaries must be preserved.
- Put exactly one blank line before a standalone comment or consecutive comment block when code
  precedes it in the enclosing block. Keep the comment adjacent to the code it describes; do not
  insert blank lines between consecutive comment lines or inside a block comment. No leading blank
  line is needed when the comment begins the block. Inline/trailing comments stay on their code line.
  A standalone comment starts a new group and prevents grouping across it, even under the three-line
  block or main-variable rules. This structural boundary takes precedence over grouping priorities.
- Different operations means pairwise-distinct operation types. Classify the complete statement,
  not every nested expression. Existing blank lines do not determine groups: recompute groups from
  these priorities within each block, switch section, or top-level statement sequence.

#### Single-line operation types

Use one primary type per operation. The following examples define distinct types unless explicitly
listed together. Resolve symbols when needed; qualification alone does not prove an instance call.

| Type                                              | Examples and classification                                                                                                                                                       |
|---------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Declaration                                       | `Service service;`                                                                                                                                                                |
| Declaration with assignment                       | `Service service = CreateService();`, `Service service = value;`, `var result = await LoadAsync();`                                                                               |
| Declaration with instance creation and assignment | `Service service = new Service();`, `Service service = new();`, `var values = new int[3];`, `var item = new { Name = name };`                                                     |
| Instance method invocation                        | `service.Initialize();`, `service?.Initialize();`, fluent calls on an instance                                                                                                    |
| Method invocation                                 | `SetProperty(ref field, value);`, `Normalize(ref name);`, static calls such as `Service.Create();`, direct delegate invocation                                                    |
| Instance event subscription                       | `MyObject.MyEvent += Handler;`, `MyObject.MyEvent += (s, e) => Console.WriteLine(s);`                                                                                             |
| Event subscription                                | `MyEvent += Handler;`, `MyEvent += (s, e) => Console.WriteLine(s);`, static event subscriptions                                                                                   |
| Instance event unsubscription                     | `MyObject.MyEvent -= Handler;`                                                                                                                                                    |
| Event unsubscription                              | `MyEvent -= Handler;`, static event unsubscriptions                                                                                                                               |
| Assignment                                        | `service = value;`, `SaveCommand = CreateAsyncRelayCommand(SaveAsync);`, `Name = value;`, `_ = SaveAsync();`, delegate assignment such as `handler = Handle;`                     |
| Instance creation and assignment                  | `service = new Service();`, `service = new();`, `AddIngredientCommand = new RelayCommand(AddIngredient);`, `values = new int[3];`                                                 |
| Compound assignment                               | `count += amount;`, `flags \|= mask;` (exclude event/delegate subscriptions)                                                                                                      |
| Null-coalescing assignment                        | `service ??= CreateService();`                                                                                                                                                    |
| Return                                            | `return service;`, `return;`, `return await LoadAsync();`                                                                                                                         |
| Exception throw                                   | `throw new Exception();`, `throw;`                                                                                                                                                |
| Increment                                         | `count++;`, `++count;`                                                                                                                                                            |
| Decrement                                         | `count--;`, `--count;`                                                                                                                                                            |
| Awaited invocation                                | `await SaveAsync();`, `await service.SaveAsync();`                                                                                                                                |
| Other await expression                            | `await pendingTask;`                                                                                                                                                              |
| Break                                             | `break;`                                                                                                                                                                          |
| Continue                                          | `continue;`                                                                                                                                                                       |
| Yield return                                      | `yield return item;`                                                                                                                                                              |
| Yield break                                       | `yield break;`                                                                                                                                                                    |
| Goto                                              | `goto retry;`, `goto case 1;`, `goto default;`                                                                                                                                    |
| Deconstruction declaration                        | `var (user, password) = GetCredentials();`, `(string user, string password) = GetCredentials();`                                                                                  |
| Deconstruction assignment                         | `(user, password) = GetCredentials();` (including mixed existing/new targets)                                                                                                     |
| Using declaration                                 | `using Stream stream = Open();`                                                                                                                                                   |
| Await-using declaration                           | `await using Stream stream = OpenAsyncStream();`                                                                                                                                  |
| Constant declaration                              | `const int limit = 10;`                                                                                                                                                           |
| Ref declaration                                   | `ref int current = ref values[0];`, `ref readonly int current = ref values[0];`                                                                                                   |
| Ref assignment                                    | `current = ref values[1];`                                                                                                                                                        |
| Standalone instance creation                      | `new Service();`                                                                                                                                                                  |
| Empty statement                                   | `;` (preserve intentional syntax; do not introduce empty statements)                                                                                                              |
| Unknown operation                                 | Any unlisted kind: report its syntax and location, use a distinct provisional type per kind, and apply the same priorities. Do not combine unrelated unknown kinds into one type. |

Classification details:

- Specialized declarations (using, await-using, const, ref, deconstruction) take precedence over the
  general declaration categories. A comma-separated declaration is one statement; if any direct
  initializer creates an instance with `new`, classify an ordinary declaration as instance-creating.
- For instance-creating declarations and instance creation and assignment, the direct initializer
  or right-hand side must be `new ...` or target-typed `new(...)` (parentheses are immaterial).
  This includes object, anonymous-object, array, and delegate creation. A nested `new` in
  `Factory(new Options())` does not qualify. Collection expressions such as `[]`, `with` expressions,
  and `stackalloc` without `new` remain declaration with assignment or assignment, respectively.
  The new assignment category applies to simple `=` assignments to existing locals, fields, or
  properties; specialized ref, deconstruction, compound, and null-coalescing assignments retain
  their own categories. Declaration with instance creation and assignment remains a distinct type.
- Outer statement classification wins: `throw new Exception()` is a throw, `return await LoadAsync()`
  is a return, and a declaration containing an awaited call remains a declaration. For standalone
  awaited calls, awaited invocation wins over instance/method invocation.
- Delegate `+=`/`-=` operations use the subscription/unsubscription categories; instance-member
  targets use the instance variants. Numeric `+=`/`-=` are compound assignments.
- Labels attach to their statements and prevent grouping across a jump target. They are not extra
  operations. Local functions and other block-owning constructs follow the structural rules above.

#### Variable threading and initialization

A main variable is the one unambiguous value being prepared, transformed, checked, or consumed across
consecutive operations. Reading or assigning it, calling its methods, or passing it as an argument
can thread it. Constants and incidental arguments (for example, options or a cancellation token)
do not disqualify a group. If two values are equally central, do not use variable-based grouping.
A coincidentally shared identifier is insufficient; inspect data flow and purpose.

A priority-4 group must start with a declaration or assignment of its main variable. Select the
variable declared or assigned by that first statement, not a variable merely read in its initializer
or right-hand side. A declaration without an initializer can start the group. Subsequent consecutive
operations must thread that same variable; do not skip unrelated operations to extend the group.
Calls or other uses alone cannot start a main-variable group. If the first statement does not identify
one unambiguous declared or assigned variable, defer to lower priorities.

For priority 1, initialization means an initialized declaration of one variable or a simple `=`
assignment/reassignment to one local, field, or property. Tuple/deconstruction or multiple-target
initializations do not qualify. A call such as `Normalize(ref name)`, an increment, a compound
assignment, or `??=` does not qualify as initialization for this exception. They can still thread
an existing main variable under priority 4. The multiline operation must clearly act on the initialized
value; an unrelated occurrence inside a nested body is insufficient. Wrapped calls qualify as well
as control-flow statements when that relationship is clear.

#### Grouping priorities

Apply each priority to the still-ungrouped operations, scanning top to bottom within that priority.
Groups cannot cross block, directive, or label boundaries. Use the longest qualifying consecutive
sequence for priorities 4 and 5; if a main variable is ambiguous, defer to lower priorities.

1. Group one multiline operation with the immediately preceding single-line initialization only when
   that initialization supplies the variable threaded by the multiline operation and the pair is
   more logically cohesive than grouping the initialization with the operations above it. Compare
   their purpose and data flow, not just a shared identifier. For example, keep related table aliases
   or SQL parameter placeholders together when they collectively prepare the following query; do not
   pull the last declaration away merely because that query uses it. If neither grouping is clearly
   stronger, leave the multiline operation separate and apply the lower priorities to the preceding
   single-line operations. A qualifying initialization/multiline pair is a complete group.
2. Each remaining multiline operation is a group of its own, separated from surrounding groups.
   Every complete multiline operation must be followed by exactly one blank line when another
   operation follows in the same block, including after a priority-1 pair. Do not add a blank line
   before the closing brace when it is last. Attached `else`, `catch`, `finally`, and trailing `while`
   clauses are part of the same operation, not following operations.
3. If the entire block consists of one to three single-line operations, group them together,
   regardless of their operation types. This is not a rule for a leftover portion of a larger block.
4. Group consecutive single-line operations around one unambiguous main variable, starting with
   its declaration or assignment. There is no three-line limit and repeated operation types are
   allowed. For example, group a declaration of
   `name`, its assignment/transformation, and successive calls consuming `name`; likewise group an
   instance initialization and three subsequent method calls on that instance.
5. Group at least two consecutive single-line operations of the same primary operation type.
6. Group remaining consecutive single-line operations with pairwise-distinct types. Scan top to
   bottom and form the longest eligible group, up to three operations. Do not absorb statements
   already grouped by higher priorities. An operation with no eligible neighbor remains a singleton.
7. Use `Recipes.sln.DotSettings` for remaining formatting details, including wrapping, indentation,
   and brace style. These documented priorities win wherever formatter settings cannot express them.

There is no separate blanket rule around single-line object initializers, assignments, returns,
subscriptions, or declaration groups. Decide their spacing using the priorities above. Validate both
missing separators and unnecessary separators; check again after wrapping changes physical line counts.

#### ReSharper output conventions

These conventions were checked against the staged/un-staged cleanup comparison on 2026-10-11.
Use the current `.editorconfig` and `Recipes.sln.DotSettings` as the executable configuration;
EditorConfig overrides overlapping solution settings. Write new and touched code close to this
output rather than relying on a later cleanup pass. The operation-grouping priorities above still
require manual review: retained blank lines in formatter output do not establish a new grouping rule.

- Use four spaces, CRLF, UTF-8 with BOM, no trailing whitespace, and a final newline as configured
  for C# files. Keep `using` directives outside the namespace, with `System` first and the remaining
  namespaces sorted. Import reordering is part of cleanup.
- For `if/else`, `for`, `foreach`, and `while`, use `required_for_multiline`: omit braces when
  the body is one physical-line statement, but add braces when that single statement wraps across
  lines, or when the body contains multiple statements. A wrapped condition alone does not require
  braces. Always put the body on the next line. Recheck braces after wrapping; preserve braces
  required by syntax, scope, or dangling-else binding.
- Keep empty constructors compact, for example `public RecipesAlias() : this(null) { }`.
  This empty-body convention does not permit nonempty control-flow bodies on their header line.
  Expand switch cases so the label, operation, and `break` occupy separate lines.
- Do not pad nonempty initializer or property-pattern braces:
  `new Options {Value = value}`, `{viewerIdParameterName, viewerId}`, `new {Name = name}`,
  and `is {Count: > 0}`. Keep the empty capture pattern `is { } value`.
- Use the configured 120-column C# wrapping target, not a mandatory break after every argument.
  Wrap long calls/signatures and ternaries at expression boundaries; put `?` and `:` on
  continuation lines for a wrapped ternary. Keep short arguments together when they fit.
  Format generated SQL within the same 120-column target, counting the raw-string indentation.
  Prefer a short, descriptive local such as `schemaSql` before passing a long script to a method;
  do not force the SQL into the narrower space of an inline argument.
  Break SQL at meaningful boundaries: clauses, complete column definitions, list items, and boolean
  conditions. Keep a column identifier with its type, a keyword with its operand, and an operator
  with its expression whenever they fit. For multiline `CREATE TABLE` column lists, put each complete
  definition on its own indented line; indent nested queries and procedural blocks consistently.
  Never use width-only word wrapping that strands `text`, `int`, `WHERE`, or `NOT VALID` on another
  line when the complete unit fits. Preserve SQL values, column order, and behavior.
- Declare local table aliases near the first SQL statement that needs them, in the narrowest scope
  that covers their later uses. Do not collect all aliases at the start of a long method. An initial
  declaration group is fine in a short method (roughly 25–30 lines) when declarations and uses can be
  seen together. Reuse an alias for later statements in the same scope rather than duplicating it.

  ```csharp
  RecipeImageUploadsAlias uploads = new();
  string schemaSql = $$"""
                       CREATE TABLE {{uploads.RecipeImageUploads}}(
                           {{uploads.FileId}} text,
                           {{uploads.OwnerId}} int,
                           {{uploads.AttachedRecipeId}} int)
                       """;

  await ExecuteAsync(source, schemaSql);
  ```

- Avoid artificial breaks between `=` and a raw-string opener or between a call and its first
  argument when the formatter keeps them together. Keep `}).ConfigureAwait(false)` together
  when it fits, and allow `}, async (reader, token) =>` on one line. Indent the initializer and
  lambda body according to their containing expression, without adding an extra level just
  because the preceding SQL literal spans many lines.
- Long primary-constructor and positional-record parameter lists use one parameter per line:
  opening parenthesis stays with the type name, parameters follow on new lines, and the closing
  parenthesis stays with the last parameter. This follows EditorConfig's `chop_if_long`,
  even where the solution settings specify a different fallback.
- Align multiline raw-string contents and closing quotes with the opening quote delimiter
  (`resharper_indent_raw_literal_string = align`), after any `$` prefix. Preserve relative
  indentation within the literal and its runtime value; do not replace alignment with a fixed
  four-space SQL indent. For example:

  ```csharp
  return $"""
          SELECT {users.Id}
          FROM {users.Users}
          """;
  ```

- Follow the custom member layout in `Recipes.sln.DotSettings`: private constants, private fields,
  protected constants/fields, public constants/fields, constructors, properties by access group,
  methods by access group, and nested types. Sort names within the configured groups. Thus SQL
  parameter constants and alias properties are alphabetical, and private `BuildSelect` helpers
  follow public provider methods rather than sitting beside the fields that call them.
  Consult the actual layout for access-group and explicit-interface details; do not invent a
  blanket order for unmatched members.
- Preserve initialization dependencies when arranging fields. For example, `ProfileReadProvider`
  initializes `summary` before `details`, because `BuildDetails()` reads `summary`. Alphabetical
  layout must not change execution order where values depend on earlier initializers. Declaration
  sorting also does not authorize reordering constructor assignments, SQL projections, reader
  ordinals, or executable statements.
- Distinguish declaration spacing from executable grouping: consecutive single-line auto-properties
  can remain adjacent, while constructors, multiline properties, and methods use the configured
  separation. Do not copy incidental extra blank lines introduced or retained by member rearrangement.
- When reviewing a user cleanup, compare the index with the working tree (`git diff`), preserve
  both versions, and distinguish formatting/member layout from semantic changes and unrelated
  configuration edits. Do not treat every unstaged change as a formatting rule.

#### Formatter integration and cleanup

Keep supported settings aligned in `Recipes.sln.DotSettings` and `.editorconfig` (EditorConfig can
override solution settings). Keep control-flow bodies on a separate line, with braces only as required
by the conventions above; preserve at most one blank line in executable code, and remove blank lines at block edges.
Variable-based grouping and the ordered exceptions require a separate review after formatter cleanup; settings alone do
not enforce them. Keep whitespace trimming and final-newline insertion enabled.

Behavior-preserving cleanup may use intention-revealing assertions such as `Assert.Contains` and
`Assert.IsGreaterThan`, readable property patterns, and suitable constant-pattern `[GeneratedRegex]`
methods with descriptive names and required partial declarations. These are code changes, not formatting:
preserve assertion argument order and regex semantics and verify them with appropriate tests.
Do not use formatting rules as permission for unrelated refactoring or altering unrelated user-staged code.
