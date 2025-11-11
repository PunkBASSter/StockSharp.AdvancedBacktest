# Generate a comprehensive Product Requirements Document (PRD) for the following feature

$ARGUMENTS

## Agent Instructions
Delegate execution to a PM Agent to create a detailed PRD for the specified feature. The PRD should be clear, structured, and cover all necessary aspects to guide the development team. No coding, no technical design—focus, instead, on defining what needs to be built.

## PRD Structure

Create a detailed PRD with these sections:

### 1. Executive Summary

- Brief overview of the feature
- Problem statement
- Proposed solution (business perspective)
- Expected impact

### 2. Goals & Objectives

- Primary goals
- Success metrics (KPIs)
- User outcomes

### 3. User Stories & Use Cases

- Assume quantative trading researchers as primary users
- Detailed trading use cases
- Data flow diagrams (if applicable)

### 4. Functional Requirements

- Core functionality (MUST have)
- Secondary features (SHOULD have)
- Out of scope (WON'T have this iteration)
- Non-functional requirements (performance, scalability, security). Excude these requirements from KPI section. Don't focus on them here.

### 5. Technical Context

- Integration points
- Data sources
- StockSharp compatibility

### 6. Timeline & Milestones

- High-level phases
- Key milestones
- Estimated effort

### 7. Open Questions

- Items requiring further discussion
- Technical unknowns

## Output Format

- Use clear headers and subheaders
- Include bullet points for readability
- Add tables where appropriate
- Be specific and actionable
- Avoid vague requirements

Create this PRD in a new file called `docs/{next-number}_PRD_{feature-name}.md`
