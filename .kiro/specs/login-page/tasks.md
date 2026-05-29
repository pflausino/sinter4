# Implementation Plan: Login Page

## Overview

Implement the SinterPrints login page as a Blazor component with isolated CSS, code-behind separation, DataAnnotations validation, and loading state. The page uses a custom empty layout, renders at "/" and "/login", and includes a branded gradient background with a centered dark card containing the login form.

## Tasks

- [x] 1. Set up LoginLayout and page structure
  - [x] 1.1 Create LoginLayout.razor in src/Web/Components/Layout
    - Create a minimal layout component that inherits LayoutComponentBase and renders only @Body
    - No sidebar, navigation, or additional chrome
    - _Requirements: 1.1, 1.2, 11.2_

  - [x] 1.2 Create Login.razor page with markup and route directives
    - Add @page "/" and @page "/login" directives
    - Add @layout LoginLayout directive
    - Structure: div.login-page > div.login-card > img.login-logo + EditForm with email/password fields + submit button
    - EditForm bound to Model with OnValidSubmit=HandleValidSubmit
    - InputText for email (type="email", placeholder="Email", maxlength=254) with label and ValidationMessage
    - InputText for password (type="password", placeholder="Senha") with label and ValidationMessage
    - Button with conditional rendering: "Entrando..." + spinner when IsSubmitting, "Entrar" otherwise
    - Button disabled attribute bound to IsSubmitting
    - No @code block — markup only
    - img element with src="images/logo.png" alt="SinterPrints"
    - _Requirements: 1.1, 2.1, 2.3, 3.1, 3.2, 3.4, 4.1, 4.3, 5.1, 6.1, 6.5, 7.1, 7.2, 7.3, 8.1, 10.1, 10.2, 11.1, 11.3, 12.2_

  - [x] 1.3 Create Login.razor.cs code-behind
    - Define partial class Login in namespace Web.Components.Pages
    - Private LoginModel Model property initialized to new()
    - Private bool IsSubmitting property initialized to false
    - Private async Task HandleValidSubmit() method: sets IsSubmitting=true, calls StateHasChanged, awaits Task.Delay(1500), sets IsSubmitting=false, calls StateHasChanged
    - Nested sealed class LoginModel with Email ([Required], [EmailAddress], [StringLength(256)]) and Password ([Required], [StringLength(128, MinimumLength=6)]) properties
    - Error messages in Portuguese: "O email é obrigatório.", "Formato de email inválido.", "A senha é obrigatória.", "A senha deve ter no mínimo 6 caracteres."
    - Must compile without nullable warnings
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 8.1, 8.2, 8.3, 8.4, 8.5, 12.1, 12.3_

  - [x] 1.4 Create Login.razor.css with isolated styles
    - .login-page: min-height 100vh, width 100vw, linear-gradient(to bottom, #0099CC, #00BFFF), flexbox centering
    - .login-card: background #1A1A1A, border-radius 12px, box-shadow, max-width 440px, width 100%, padding 40px, color #FFFFFF
    - Input fields: 1px solid #00BFFF border, transparent background, white text
    - .btn-entrar: background #FF0066, color #FFFFFF, width 100%, transition 300ms; hover state background #FFCC00
    - .btn-entrar:disabled: opacity 0.6, cursor not-allowed
    - .login-logo: max-width 80%, max-height 120px, centered, object-fit contain
    - Responsive @media (max-width: 576px): card width 92%, padding 24px
    - Vertical gap of at least 16px between form elements
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.2, 2.3, 2.4, 2.5, 3.3, 4.2, 5.2, 6.2, 6.3, 6.4, 6.5, 7.1, 9.1, 9.2, 10.2, 10.3_

  - [x] 1.5 Place logo placeholder at wwwroot/images/logo.png
    - Create a placeholder PNG image file (can be a minimal valid PNG)
    - Ensures the static file path exists for development
    - _Requirements: 3.2_

- [x] 2. Handle existing route conflict
  - [x] 2.1 Remove or update Home.razor route
    - Check if src/Web/Components/Pages/Home.razor exists with @page "/"
    - If it does, remove the "/" route from Home.razor (or remove the file if it's the default template)
    - The login page takes over as the default landing page
    - _Requirements: 11.1, 11.3_

- [x] 3. Checkpoint - Verify build and rendering
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Write tests for Login component
  - [x] 4.1 Create Web.Tests project (if not exists) with bUnit and FsCheck dependencies
    - Add xUnit, bUnit, FsCheck, and FsCheck.Xunit NuGet packages
    - Add project reference to src/Web
    - Ensure the test project targets net10.0
    - _Requirements: 8.1_

  - [x] 4.2 Write bUnit tests for component rendering and behavior
    - Test: login page renders logo, email input, password input, and Entrar button
    - Test: email input has type="email" and associated label
    - Test: submitting empty form shows validation error messages
    - Test: submitting with valid data invokes OnValidSubmit without validation messages
    - Test: button shows "Entrando..." and is disabled when IsSubmitting is true
    - Test: button shows "Entrar" when IsSubmitting is false
    - _Requirements: 4.1, 4.4, 4.5, 5.1, 5.3, 5.4, 6.1, 7.1, 7.2, 7.3, 8.4, 8.5, 10.1_

  - [x] 4.3 Write property test for LoginModel valid inputs
    - **Property 1: LoginModel validation accepts only valid inputs**
    - Generate random valid email/password pairs (email with valid format ≤256 chars, password 6–128 chars)
    - Assert Validator.TryValidateObject returns zero errors
    - **Validates: Requirements 8.4, 8.5**

  - [x] 4.4 Write property test for invalid email rejection
    - **Property 2: Invalid email format is rejected**
    - Generate random strings that are not valid email addresses
    - Assert validation fails with email-format error message
    - **Validates: Requirements 4.5, 8.4**

  - [x] 4.5 Write property test for short password rejection
    - **Property 3: Short password is rejected**
    - Generate random non-empty strings with length 1–5
    - Assert validation fails with minimum-length error message
    - **Validates: Requirements 5.4, 8.4**

  - [x] 4.6 Write property test for loading state idempotence
    - **Property 4: Loading state disables submission**
    - Verify that when IsSubmitting is true, calling HandleValidSubmit does not initiate a new submission
    - **Validates: Requirements 7.1, 7.4**

  - [x] 4.7 Write property test for submit round-trip state restoration
    - **Property 5: Submit round-trip restores initial state**
    - Trigger valid submit, await completion, assert IsSubmitting returns to false
    - **Validates: Requirements 7.3**

- [x] 5. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests (bUnit) validate specific rendering and interaction behavior
- The design uses C# with Blazor (.NET 10) — no language selection needed
- All styles are isolated via Login.razor.css — no global CSS modifications
- The LoginModel is a nested sealed class inside the Login partial class

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.5", "2.1"] },
    { "id": 1, "tasks": ["1.3", "1.4"] },
    { "id": 2, "tasks": ["1.2"] },
    { "id": 3, "tasks": ["4.1"] },
    { "id": 4, "tasks": ["4.2", "4.3", "4.4", "4.5", "4.6", "4.7"] }
  ]
}
```
