---
title: ProjGraph
layout: landing
---

<section class="pg-hero">
<h1>Your code already describes its architecture. ProjGraph draws it.</h1>
<p class="pg-hero-lede">One command turns a solution, an EF Core <code>DbContext</code>, or a single class into a Mermaid diagram or an ASCII tree. Run it from your terminal, or let an AI assistant run it for you over MCP.</p>
<div class="pg-cta">
<a class="pg-btn pg-btn-primary" href="guides/getting-started.md">Install the CLI</a>
<a class="pg-btn" href="guides/getting-started.md#use-it-from-an-ai-assistant">Set up the MCP server</a>
<a class="pg-btn" href="https://github.com/HandyS11/ProjGraph">View source</a>
</div>
</section>

<section class="pg-transform">
<div class="pg-pane">
<div class="pg-pane-bar"><span>LibraryContext.cs</span><span class="pg-pane-tag">input</span></div>
<pre><span class="pg-cs-key">public class</span> <span class="pg-cs-type">LibraryContext</span> : <span class="pg-cs-type">DbContext</span>
{
    <span class="pg-cs-key">public</span> <span class="pg-cs-type">DbSet</span>&lt;<span class="pg-cs-type">Author</span>&gt;    Authors    { <span class="pg-cs-key">get</span>; <span class="pg-cs-key">set</span>; }
    <span class="pg-cs-key">public</span> <span class="pg-cs-type">DbSet</span>&lt;<span class="pg-cs-type">Book</span>&gt;      Books      { <span class="pg-cs-key">get</span>; <span class="pg-cs-key">set</span>; }
    <span class="pg-cs-key">public</span> <span class="pg-cs-type">DbSet</span>&lt;<span class="pg-cs-type">Publisher</span>&gt; Publishers { <span class="pg-cs-key">get</span>; <span class="pg-cs-key">set</span>; }
    <span class="pg-cs-key">public</span> <span class="pg-cs-type">DbSet</span>&lt;<span class="pg-cs-type">Review</span>&gt;    Reviews    { <span class="pg-cs-key">get</span>; <span class="pg-cs-key">set</span>; }
}
<span class="pg-cs-key">public class</span> <span class="pg-cs-type">Book</span>
{
    <span class="pg-cs-key">public</span> <span class="pg-cs-key">int</span>            Id          { <span class="pg-cs-key">get</span>; <span class="pg-cs-key">set</span>; }
    [<span class="pg-cs-type">MaxLength</span>(300)]
    <span class="pg-cs-key">public</span> <span class="pg-cs-key">string</span>         Title       { <span class="pg-cs-key">get</span>; <span class="pg-cs-key">set</span>; }
    <span class="pg-cs-key">public</span> <span class="pg-cs-key">int</span>            PublisherId { <span class="pg-cs-key">get</span>; <span class="pg-cs-key">set</span>; }
    <span class="pg-cs-key">public</span> <span class="pg-cs-type">Publisher</span>      Publisher   { <span class="pg-cs-key">get</span>; <span class="pg-cs-key">set</span>; }
    <span class="pg-cs-key">public</span> <span class="pg-cs-type">List</span>&lt;<span class="pg-cs-type">Review</span>&gt;   Reviews     { <span class="pg-cs-key">get</span>; <span class="pg-cs-key">set</span>; }
}</pre>
</div>
<div class="pg-pane pg-pane-out">
<div class="pg-pane-bar"><span>projgraph erd</span><span class="pg-pane-tag">output</span></div>
<svg class="pg-graph" viewBox="0 0 340 196" role="img" aria-label="Entity relationship diagram generated from LibraryContext: Author and Publisher each relate to Book, and Book relates to Review.">
<path class="pg-g-edge" d="M122,44 H200" />
<path class="pg-g-edge" d="M122,144 C158,144 168,62 200,62" />
<path class="pg-g-edge" d="M259,76 V126" />
<rect class="pg-g-box" x="18" y="18" width="104" height="52" rx="5" />
<text class="pg-g-head" x="27" y="33">Author</text>
<path class="pg-g-edge" d="M18,39 H122" style="stroke-width:0.7;stroke:var(--pg-hairline-strong)" />
<text class="pg-g-field" x="27" y="51">Id <tspan class="pg-g-pk">PK</tspan></text>
<text class="pg-g-field" x="27" y="62">Name</text>
<rect class="pg-g-box" x="200" y="14" width="118" height="62" rx="5" />
<text class="pg-g-head" x="209" y="29">Book</text>
<path class="pg-g-edge" d="M200,35 H318" style="stroke-width:0.7;stroke:var(--pg-hairline-strong)" />
<text class="pg-g-field" x="209" y="47">Id <tspan class="pg-g-pk">PK</tspan></text>
<text class="pg-g-field" x="209" y="58">Title <tspan class="pg-g-pk">max:300</tspan></text>
<text class="pg-g-field" x="209" y="69">PublisherId <tspan class="pg-g-pk">FK</tspan></text>
<rect class="pg-g-box" x="18" y="118" width="104" height="52" rx="5" />
<text class="pg-g-head" x="27" y="133">Publisher</text>
<path class="pg-g-edge" d="M18,139 H122" style="stroke-width:0.7;stroke:var(--pg-hairline-strong)" />
<text class="pg-g-field" x="27" y="151">Id <tspan class="pg-g-pk">PK</tspan></text>
<text class="pg-g-field" x="27" y="162">Name</text>
<rect class="pg-g-box" x="200" y="126" width="118" height="52" rx="5" />
<text class="pg-g-head" x="209" y="141">Review</text>
<path class="pg-g-edge" d="M200,147 H318" style="stroke-width:0.7;stroke:var(--pg-hairline-strong)" />
<text class="pg-g-field" x="209" y="159">Id <tspan class="pg-g-pk">PK</tspan></text>
<text class="pg-g-field" x="209" y="170">Rating</text>
<circle class="pg-g-node" cx="122" cy="44" r="2.2" />
<circle class="pg-g-node" cx="200" cy="44" r="2.2" />
<circle class="pg-g-node" cx="122" cy="144" r="2.2" />
<circle class="pg-g-node" cx="200" cy="62" r="2.2" />
<circle class="pg-g-node" cx="259" cy="76" r="2.2" />
<circle class="pg-g-node" cx="259" cy="126" r="2.2" />
<text class="pg-g-card" x="128" y="40">1</text>
<text class="pg-g-card" x="190" y="40">n</text>
<text class="pg-g-card" x="128" y="140">1</text>
<text class="pg-g-card" x="190" y="58">n</text>
<text class="pg-g-card" x="264" y="88">1</text>
<text class="pg-g-card" x="264" y="122">n</text>
</svg>
</div>
</section>

<p class="pg-transform-note">Nothing was annotated to produce this. <b>ProjGraph</b> reads the C# with Roslyn, resolves the navigation properties into relationships, and carries the constraints across &mdash; <code>[MaxLength(300)]</code> on the left becomes <code>max:300</code> on the right. No build, no running database, no migration step.</p>

<div class="pg-install">
<span class="pg-prompt">$</span>
<code>dotnet tool install -g ProjGraph.Cli</code>
<button class="pg-copy" type="button">Copy</button>
</div>

<section class="pg-section">
<h2>What it draws</h2>
<p>Four commands, each pointed at something you already have on disk.</p>
<div class="pg-caps">
<div class="pg-cap"><h3>projgraph visualize</h3><p>Reads a <code>.slnx</code>, <code>.sln</code>, or <code>.csproj</code> and maps how the projects reference each other. Emits Mermaid by default, or an ASCII tree for the terminal.</p></div>
<div class="pg-cap"><h3>projgraph erd</h3><p>Turns an EF Core <code>DbContext</code> or a <code>ModelSnapshot</code> into an entity relationship diagram, including keys, constraints, owned types, and join tables.</p></div>
<div class="pg-cap"><h3>projgraph classdiagram</h3><p>Draws a class with its inheritance chain and dependencies, following related types across the workspace as deep as you ask it to.</p></div>
<div class="pg-cap"><h3>projgraph stats</h3><p>Reports project counts, type breakdown, dependency depth, and the hotspot projects that everything else references.</p></div>
</div>
</section>

<section class="pg-section">
<h2>Two ways to run it</h2>
<p>The same analysis, reached either by hand or by an assistant.</p>
<div class="pg-runners">
<div class="pg-runner">
<h3>Command line</h3>
<pre>$ projgraph erd ./Data/LibraryContext.cs
$ projgraph visualize ./MySolution.slnx --format mermaid
$ projgraph stats ./MySolution.slnx --top 10</pre>
<p>Writes to stdout by default, or to a file with <code>--output</code>. The Mermaid it emits renders as-is on GitHub and GitLab.</p>
<p class="pg-runner-link"><a href="../src/ProjGraph.Cli/README.md">CLI reference</a></p>
</div>
<div class="pg-runner">
<h3>AI assistant</h3>
<pre>{
  "servers": {
    "ProjGraph.Mcp": {
      "type": "stdio",
      "command": "dnx",
      "args": ["ProjGraph.Mcp@x.x.x", "--yes"]
    }
  }
}</pre>
<p>Drop this into any MCP client &mdash; GitHub Copilot, Claude, and others &mdash; then ask about your architecture in plain language.</p>
<p class="pg-runner-link"><a href="../src/ProjGraph.Mcp/README.md">MCP reference</a></p>
</div>
</div>
</section>

<section class="pg-section">
<h2>See it on real code</h2>
<p>Every diagram below was generated by ProjGraph from a project in this repository, and is regenerated whenever the tool changes.</p>
<div class="pg-shots">
<a class="pg-shot" href="../samples/erd/complex-ecommerce/README.md"><strong>E-commerce schema</strong><span>Twelve entities, table-per-hierarchy, and recursive foreign keys.</span></a>
<a class="pg-shot" href="../samples/classdiagram/design-patterns/README.md"><strong>Design patterns</strong><span>Creational, structural, and behavioural patterns drawn from source.</span></a>
<a class="pg-shot" href="../samples/visualize/modular-architecture/README.md"><strong>Modular solution</strong><span>A layered <code>.slnx</code> workspace and its reference graph.</span></a>
<a class="pg-shot" href="../samples/stats/README.md"><strong>Solution metrics</strong><span>Counts, depth, and hotspots for a real solution.</span></a>
</div>
</section>

<section class="pg-section">
<h2>Where to go next</h2>
<div class="pg-next">
<a href="guides/getting-started.md"><strong>Getting started</strong><span>Install ProjGraph and draw your first diagram.</span></a>
<a href="guides/output-formats.md"><strong>Output formats</strong><span>Choose between ASCII tree, Mermaid, and Markdown.</span></a>
<a href="guides/troubleshooting.md"><strong>Troubleshooting</strong><span>What to do when a diagram comes back empty.</span></a>
<a href="../samples/README.md"><strong>Showcase</strong><span>Every sample, with its command and its output.</span></a>
<a href="../ARCHITECTURE.md"><strong>Architecture</strong><span>How the libraries and entry points fit together.</span></a>
<a href="xref:ProjGraph.Core.Models"><strong>API reference</strong><span>Generated documentation for every public type.</span></a>
</div>
</section>
