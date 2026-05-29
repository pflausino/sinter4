# Requirements Document

## Introduction

This document defines the requirements for integrating Firebase Authentication into the SinterPrints application. Firebase serves as the sole identity provider — all user accounts are created exclusively by administrators (no self-registration). The backend validates Firebase JWT tokens on every API request to ensure only authenticated users access protected endpoints. This spec covers the backend authentication middleware, Firebase Admin SDK initialization (for future admin features), frontend authentication flow (connecting the existing login page to Firebase), logout/session management, route protection, and configuration management.

## Glossary

- **Firebase_Auth**: The Firebase Authentication service used as the external identity provider for SinterPrints
- **JWT_Token**: A JSON Web Token issued by Firebase after successful user authentication, containing user identity claims
- **Auth_Middleware**: The ASP.NET Core authentication middleware that validates Firebase JWT tokens on incoming API requests
- **FirebaseAdmin_SDK**: The Firebase Admin .NET SDK used server-side, initialized at startup for future administrative operations
- **Auth_Service**: The application service in the Infrastructure layer responsible for wrapping FirebaseAdmin SDK operations (reserved for future admin features)
- **Login_Page**: The existing Blazor login page that collects email/password credentials from the user
- **Token_Provider**: The frontend service that calls the Firebase REST API to exchange email/password for a JWT token
- **Auth_State_Provider**: A Blazor `AuthenticationStateProvider` implementation that tracks the current user's authentication state based on the Firebase token
- **Protected_Endpoint**: An API endpoint or Blazor page that requires a valid JWT_Token to access
- **Authorization_Policy**: A named ASP.NET Core policy that restricts endpoint access to authenticated users with a valid JWT_Token

## Requirements

### Requirement 1: Firebase JWT Token Validation

**User Story:** As a developer, I want the API to validate Firebase JWT tokens on every request, so that only authenticated users can access protected endpoints.

#### Acceptance Criteria

1. THE Auth_Middleware SHALL validate the `Authorization: Bearer <token>` header on every request to a Protected_Endpoint, using Firebase's public keys and issuer, while allowing requests to endpoints without an authorization requirement to pass through without token validation
2. WHEN a request contains a valid, non-expired Firebase JWT_Token, THE Auth_Middleware SHALL extract the user's UID and email and populate the `HttpContext.User` ClaimsPrincipal with corresponding claim types
3. WHEN a request contains an expired JWT_Token, THE Auth_Middleware SHALL return HTTP 401 Unauthorized with a JSON body containing a field `error` set to `"token_expired"` and a field `message` with a human-readable description
4. WHEN a request contains a malformed or invalid JWT_Token, THE Auth_Middleware SHALL return HTTP 401 Unauthorized with a JSON body containing a field `error` set to `"invalid_token"` and a field `message` with a human-readable description
5. WHEN a request to a Protected_Endpoint contains no Authorization header, THE Auth_Middleware SHALL return HTTP 401 Unauthorized with a JSON body containing a field `error` set to `"missing_token"` and a field `message` with a human-readable description
6. THE Auth_Middleware SHALL read the Firebase project ID from application configuration (`Firebase:ProjectId`) and use it to validate that the token's `aud` claim equals the project ID and the `iss` claim equals `https://securetoken.google.com/{ProjectId}`
7. THE Auth_Middleware SHALL allow a clock skew tolerance of no more than 5 minutes when validating token expiration

### Requirement 2: Firebase Admin SDK Initialization

**User Story:** As a developer, I want the FirebaseAdmin SDK initialized at application startup, so that the infrastructure is ready for future administrative operations.

#### Acceptance Criteria

1. WHEN the application starts, THE Infrastructure SHALL initialize the FirebaseAdmin_SDK using a service account credential file path read from configuration (`Firebase:ServiceAccountPath`)
2. THE Infrastructure SHALL register the FirebaseAdmin_SDK as a singleton in the DI container via the existing `AddInfrastructure` extension method, ensuring only one FirebaseApp instance is created regardless of how many services depend on it
3. IF the service account credential file path is configured but the file does not exist at that path, THEN THE Infrastructure SHALL throw an exception whose message contains the attempted file path and indicates the file was not found, preventing the application from starting
4. IF the service account credential file exists but contains invalid content (malformed JSON or missing required service account fields), THEN THE Infrastructure SHALL throw an exception whose message contains the file path and indicates the credential format is invalid, preventing the application from starting
5. THE Infrastructure SHALL expose the Auth_Service as a scoped service in the DI container, providing a foundation for future administrative operations

### Requirement 3: Frontend Authentication Flow

**User Story:** As a user, I want to sign in on the login page and have my session maintained, so that I can access the application without re-entering credentials on every page.

#### Acceptance Criteria

1. WHEN the user submits valid credentials on the Login_Page, THE Token_Provider SHALL send the email and password to the Firebase Authentication REST API and obtain a JWT_Token and a refresh token
2. WHEN Firebase returns a valid JWT_Token, THE Token_Provider SHALL store the JWT_Token and the refresh token in protected browser storage and notify the Auth_State_Provider of the authenticated state
3. WHEN Firebase returns an authentication error (invalid credentials, user disabled), THE Login_Page SHALL display a localized error message to the user without exposing internal error codes or stack traces, and SHALL restore the submit button to its default enabled state
4. IF the Token_Provider does not receive a response from the Firebase Authentication REST API within 15 seconds, THEN THE Login_Page SHALL display an error message indicating a connectivity problem and restore the submit button to its default enabled state
5. THE Auth_State_Provider SHALL expose the current user's authentication state (authenticated or anonymous, and email) to all Blazor components via the `CascadingAuthenticationState`
6. WHEN the Token_Provider is about to make an API call and the stored JWT_Token is within 5 minutes of expiration, THE Token_Provider SHALL attempt to refresh the token using the stored refresh token, retrying up to 2 times with a 2-second delay between attempts
7. WHEN token refresh fails after all retry attempts are exhausted, THE Auth_State_Provider SHALL transition the user to an unauthenticated state and redirect to the Login_Page

### Requirement 4: Logout and Session Termination

**User Story:** As a user, I want to log out of the application, so that my session is terminated and credentials are cleared.

#### Acceptance Criteria

1. WHEN the user triggers a logout action, THE Token_Provider SHALL clear the stored JWT_Token and refresh token from browser storage before any navigation occurs
2. WHEN the user triggers a logout action, THE Auth_State_Provider SHALL transition to an unauthenticated state and redirect the user to the Login_Page within 1 second of the action
3. WHILE the user is in an unauthenticated state, WHEN the user attempts to navigate to a protected page, THE application SHALL redirect to the Login_Page without making any API calls with the cleared token
4. IF the Token_Provider fails to clear tokens from browser storage during logout, THEN THE Auth_State_Provider SHALL still transition to an unauthenticated state and redirect the user to the Login_Page

### Requirement 5: Route Protection and Authorization

**User Story:** As a developer, I want protected routes to require authentication, so that unauthenticated users cannot access restricted pages.

#### Acceptance Criteria

1. WHEN an unauthenticated user navigates to a protected Blazor page, THE application SHALL redirect to the Login_Page without rendering the protected page content
2. WHILE the Auth_State_Provider is resolving the user's authentication state, THE application SHALL display a loading indicator and SHALL NOT render the protected page content or redirect to the Login_Page
3. THE application SHALL use Blazor's `AuthorizeRouteView` component to enforce route-level authorization based on the Auth_State_Provider
4. THE application SHALL support the `[Authorize]` attribute on Blazor pages to mark them as requiring authentication
5. THE Authorization_Policy named "Authenticated" SHALL require any valid JWT_Token, granting access to all authenticated users regardless of claims

### Requirement 6: Configuration and Secrets Management

**User Story:** As a developer, I want Firebase configuration managed via standard ASP.NET Core configuration, so that secrets are never committed to source control.

#### Acceptance Criteria

1. THE application SHALL read Firebase configuration from the `Firebase` section in `appsettings.json` containing the required keys `ProjectId` and `ServiceAccountPath`
2. THE application SHALL support overriding Firebase configuration values via environment variables following ASP.NET Core's convention (`Firebase__ProjectId`, `Firebase__ServiceAccountPath`)
3. WHILE the application is running in the Development environment, THE application SHALL load configuration from .NET User Secrets, allowing the service account file path to be stored outside committed configuration files
4. IF a required Firebase configuration key (`ProjectId` or `ServiceAccountPath`) is missing or empty at startup, THEN THE application SHALL throw an exception within 5 seconds of startup that includes the name of the missing configuration key and the configuration section path (`Firebase:<KeyName>`)
5. IF the `ServiceAccountPath` configuration value is present but the file at that path does not exist or is not readable, THEN THE application SHALL throw a descriptive exception at startup identifying the invalid file path
6. THE application SHALL commit `appsettings.json` with empty string values for all Firebase secret-related keys, ensuring no actual credentials or service account file paths are present in source control

### Requirement 7: Integration with Existing Login Page

**User Story:** As a developer, I want the existing login page to connect to Firebase Authentication, so that the visual UI already built becomes functional.

#### Acceptance Criteria

1. WHEN the Login_Page form is submitted with valid data (passing all DataAnnotation validations on Login_Model), THE Login_Page SHALL call the Token_Provider service with the submitted email and password instead of the current simulated delay
2. WHILE the Token_Provider is authenticating, THE Login_Page SHALL maintain the existing loading state (disabled button, "Entrando..." text, spinner) as defined in the login-page spec
3. WHEN authentication succeeds and the Token_Provider returns a valid JWT_Token, THE Login_Page SHALL navigate the user to the "/dashboard" route
4. WHEN authentication fails due to invalid credentials or a disabled account, THE Login_Page SHALL display a localized error message below the form fields, above the submit button, and restore the Entrar_Button to its default state
5. IF the Token_Provider encounters a network error or an unexpected failure, THEN THE Login_Page SHALL display a generic error message indicating the service is unavailable, and restore the Entrar_Button to its default state
6. WHEN the user modifies the email or password field after an error is displayed, THE Login_Page SHALL clear the previously displayed error message
7. IF the Token_Provider does not respond within 30 seconds, THEN THE Login_Page SHALL abort the authentication attempt, display a timeout error message, and restore the Entrar_Button to its default state
8. THE Login_Page SHALL retain all existing visual styling, validation behavior, and accessibility attributes defined in the login-page spec, including the gradient background, card layout, input field styling, and ARIA attributes
