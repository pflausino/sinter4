# Implementation Plan: Firebase Authentication

## Overview

Integrate Firebase Authentication into SinterPrints as the sole identity provider. This plan covers: Firebase Admin SDK initialization in the Infrastructure layer, JWT Bearer authentication middleware in the API, frontend authentication flow (token provider, auth state provider), login page integration, logout/session management, route protection, and configuration/secrets management. All code is C# targeting .NET 10.

## Tasks

- [x] 1. Firebase Admin SDK initialization and Infrastructure setup
  - [x] 1.1 Add FirebaseAdmin NuGet package to `src/Infrastructure/Infrastructure.csproj` and create `src/Infrastructure/Auth/FirebaseInitializer.cs`
    - Add `FirebaseAdmin` package reference
    - Implement static `Initialize(IConfiguration)` method that reads `Firebase:ServiceAccountPath`, validates file existence, loads `GoogleCredential`, and calls `FirebaseApp.Create()`
    - Throw descriptive exceptions for missing config key, missing file, and invalid credentials
    - _Requirements: 2.1, 2.3, 2.4, 6.4, 6.5_

  - [x] 1.2 Create `src/Infrastructure/Auth/IAuthService.cs` and `src/Infrastructure/Auth/FirebaseAuthService.cs`
    - Define `IAuthService` interface (empty placeholder for future admin operations)
    - Implement `FirebaseAuthService` as internal empty class
    - _Requirements: 2.5_

  - [x] 1.3 Update `src/Infrastructure/DependencyInjection.cs` to call `FirebaseInitializer.Initialize()` and register `IAuthService`
    - Call `FirebaseInitializer.Initialize(configuration)` inside `AddInfrastructure`
    - Register `IAuthService` as scoped with `FirebaseAuthService` implementation
    - _Requirements: 2.1, 2.2, 2.5_

  - [ ]* 1.4 Write property test for startup credential file validation
    - **Property 5: Startup credential file validation**
    - **Validates: Requirements 2.3, 2.4, 6.5**

  - [ ]* 1.5 Write property test for missing configuration key exception
    - **Property 9: Missing configuration key exception identifies the key**
    - **Validates: Requirements 6.4**

- [x] 2. API JWT Bearer authentication and authorization setup
  - [x] 2.1 Add authentication NuGet packages to `src/Api/Api.csproj` and configure JWT Bearer in `src/Api/Program.cs`
    - Add `Microsoft.AspNetCore.Authentication.JwtBearer` package
    - Read `Firebase:ProjectId` from configuration, throw if missing
    - Configure `AddAuthentication().AddJwtBearer()` with Firebase issuer, audience, and 5-minute clock skew
    - Add `app.UseAuthentication()` and `app.UseAuthorization()` to the pipeline
    - _Requirements: 1.1, 1.6, 1.7, 6.1, 6.4_

  - [x] 2.2 Implement custom `JwtBearerEvents` for structured 401 JSON responses
    - Handle `OnChallenge` event to produce JSON with `error` and `message` fields
    - Map `SecurityTokenExpiredException` → `"token_expired"`, present but invalid → `"invalid_token"`, missing header → `"missing_token"`
    - _Requirements: 1.3, 1.4, 1.5_

  - [x] 2.3 Add `"Authenticated"` authorization policy via `AddAuthorizationBuilder()`
    - Single policy: `RequireAuthenticatedUser()`
    - Apply policy to protected endpoint groups
    - _Requirements: 5.5_

  - [ ]* 2.4 Write property test for valid JWT claim extraction
    - **Property 1: Valid JWT claim extraction preserves token identity**
    - **Validates: Requirements 1.2**

  - [ ]* 2.5 Write property test for malformed token rejection
    - **Property 2: Malformed tokens are uniformly rejected**
    - **Validates: Requirements 1.4**

  - [ ]* 2.6 Write property test for audience and issuer validation
    - **Property 3: Token audience and issuer validation**
    - **Validates: Requirements 1.6**

  - [ ]* 2.7 Write property test for clock skew boundary enforcement
    - **Property 4: Clock skew boundary enforcement**
    - **Validates: Requirements 1.7**

  - [ ]* 2.8 Write property test for Authenticated policy evaluation
    - **Property 8: Authenticated policy accepts any authenticated user**
    - **Validates: Requirements 5.5**

- [x] 3. Checkpoint - Ensure backend compiles and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Frontend authentication services
  - [x] 4.1 Create `src/Web/Services/ITokenProvider.cs` with `AuthResult` record
    - Define `SignInAsync`, `GetTokenAsync`, `SignOutAsync`, `RefreshTokenAsync` methods
    - Define `AuthResult(bool Success, string? Token, string? ErrorMessage)` record
    - _Requirements: 3.1, 3.2_

  - [x] 4.2 Create `src/Web/Services/FirebaseTokenProvider.cs` implementing `ITokenProvider`
    - Use `HttpClient` to call Firebase REST API (`identitytoolkit.googleapis.com`) for sign-in
    - Use `securetoken.googleapis.com` for token refresh
    - Store tokens in `ProtectedLocalStorage`
    - 15-second HTTP timeout, retry refresh up to 2 times with 2-second delay
    - Map Firebase error codes to localized user-friendly messages (Portuguese)
    - Read `Firebase:ApiKey` from configuration
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.6_

  - [x] 4.3 Create `src/Web/Services/FirebaseAuthStateProvider.cs` extending `AuthenticationStateProvider`
    - Read token from `ProtectedLocalStorage` on initialization
    - Parse JWT claims to build `ClaimsPrincipal`
    - Expose `NotifyAuthenticationStateChanged` when tokens change
    - Check token expiration; trigger refresh if within 5 minutes
    - On refresh failure, clear state and redirect to `/login`
    - _Requirements: 3.5, 3.6, 3.7_

  - [x] 4.4 Register authentication services in `src/Web/Program.cs`
    - Register `ITokenProvider` as scoped with `FirebaseTokenProvider`
    - Register `AuthenticationStateProvider` as scoped with `FirebaseAuthStateProvider`
    - Add `CascadingAuthenticationState` to the component tree
    - Configure named `HttpClient` for Firebase REST API calls
    - _Requirements: 3.5_

  - [ ]* 4.5 Write property test for Firebase error code mapping
    - **Property 6: Firebase error codes map to user-friendly messages**
    - **Validates: Requirements 3.3**

  - [ ]* 4.6 Write property test for token refresh timing
    - **Property 7: Token refresh triggers within expiration window**
    - **Validates: Requirements 3.6**

  - [ ]* 4.7 Write unit tests for `FirebaseTokenProvider` (sign-in, refresh, timeout scenarios)
    - Test successful sign-in stores tokens and returns success
    - Test invalid credentials returns localized error
    - Test timeout after 15 seconds returns connectivity error
    - Test refresh retry logic (2 retries, 2-second delay)
    - _Requirements: 3.1, 3.3, 3.4, 3.6_

- [x] 5. Checkpoint - Ensure frontend services compile and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Login page integration and logout
  - [x] 6.1 Update `src/Web/Components/Pages/Login.razor.cs` to use `ITokenProvider`
    - Inject `ITokenProvider` and `NavigationManager`
    - Replace simulated delay with `TokenProvider.SignInAsync(email, password)`
    - Navigate to `/dashboard` on success
    - Display localized error messages on failure (invalid credentials, disabled account, network error, timeout)
    - Add 30-second `CancellationTokenSource` for timeout handling
    - Clear error message on field modification (`OnFieldChanged`)
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7, 7.8_

  - [x] 6.2 Implement logout in `ITokenProvider.SignOutAsync` and wire to UI
    - Clear JWT and refresh token from `ProtectedLocalStorage`
    - Notify `FirebaseAuthStateProvider` to transition to unauthenticated state
    - Redirect to `/login` within 1 second
    - Handle storage clear failures gracefully (still transition state)
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [ ]* 6.3 Write unit tests for Login page integration (bUnit)
    - Test form submission calls `ITokenProvider.SignInAsync`
    - Test navigation to `/dashboard` on success
    - Test error message display on failure
    - Test error clearing on field change
    - Test timeout behavior at 30 seconds
    - _Requirements: 7.1, 7.3, 7.4, 7.5, 7.6, 7.7_

- [x] 7. Route protection with AuthorizeRouteView
  - [x] 7.1 Configure `AuthorizeRouteView` in `src/Web/Components/Routes.razor` (or equivalent app root)
    - Replace `RouteView` with `AuthorizeRouteView`
    - Configure `NotAuthorized` to redirect to `/login`
    - Configure `Authorizing` to show a loading indicator
    - _Requirements: 5.1, 5.2, 5.3_

  - [x] 7.2 Add `[Authorize]` attribute to protected pages and ensure `/login` remains public
    - Mark pages that require authentication with `@attribute [Authorize]`
    - Ensure Login page and any public pages do NOT have the attribute
    - _Requirements: 5.1, 5.4_

- [x] 8. Configuration and secrets management
  - [x] 8.1 Update `src/Api/appsettings.json` with empty Firebase configuration section
    - Add `"Firebase": { "ProjectId": "", "ServiceAccountPath": "", "ApiKey": "" }` with empty values
    - _Requirements: 6.1, 6.6_

  - [x] 8.2 Update `src/Web/appsettings.json` (or shared config) with Firebase `ApiKey` placeholder
    - Add `"Firebase": { "ApiKey": "" }` with empty value for frontend use
    - _Requirements: 6.1, 6.6_

  - [x] 8.3 Document User Secrets setup in a README or code comment for development environment
    - Indicate how to set `Firebase:ProjectId`, `Firebase:ServiceAccountPath`, and `Firebase:ApiKey` via `dotnet user-secrets`
    - _Requirements: 6.2, 6.3_

- [x] 9. Final checkpoint - Ensure full solution compiles and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The design uses C# (.NET 10) throughout — no language selection needed
- No admin endpoints, role management, or user management in this version

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "4.1", "8.1", "8.2"] },
    { "id": 1, "tasks": ["1.2", "2.1", "4.2", "8.3"] },
    { "id": 2, "tasks": ["1.3", "2.2", "4.3"] },
    { "id": 3, "tasks": ["1.4", "1.5", "2.3", "4.4"] },
    { "id": 4, "tasks": ["2.4", "2.5", "2.6", "2.7", "2.8", "4.5", "4.6", "4.7"] },
    { "id": 5, "tasks": ["6.1", "7.1"] },
    { "id": 6, "tasks": ["6.2", "7.2"] },
    { "id": 7, "tasks": ["6.3"] }
  ]
}
```
