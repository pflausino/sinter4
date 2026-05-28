# Requirements Document

## Introduction

This document defines the requirements for the SinterPrints login page — the initial screen users see when accessing the application. The login page provides a visually branded entry point with email/password fields and client-side validation. It does not integrate with Firebase Authentication yet; this spec covers only the visual structure, layout, and form validation behavior.

## Glossary

- **Login_Page**: The Blazor page component rendered at the application's root or "/login" route, containing the login card and background
- **Login_Card**: The centered dark-background container holding the logo, input fields, and submit button
- **Login_Form**: The EditForm component inside the Login_Card that handles field validation via DataAnnotations
- **Entrar_Button**: The submit button labeled "Entrar" that triggers form validation
- **Login_Model**: The data model class backing the Login_Form with Email and Password properties

## Requirements

### Requirement 1: Page Background Gradient

**User Story:** As a user, I want the login page to have a branded gradient background, so that the application feels professional and visually aligned with the SinterPrints identity.

#### Acceptance Criteria

1. THE Login_Page SHALL render a full-viewport background with a vertical linear gradient from #0099CC at the top to #00BFFF at the bottom, covering the entire viewport without repeating
2. THE Login_Page SHALL occupy 100% of the viewport height (100vh) and 100% of the viewport width (100vw) with no visible scrollbars while all card content fits within the viewport boundaries
3. IF the page content exceeds the viewport height, THEN THE Login_Page SHALL allow vertical scrolling while maintaining the gradient background covering the full scrollable area

### Requirement 2: Centered Login Card

**User Story:** As a user, I want the login form presented in a centered card with dark background, so that it stands out clearly against the gradient and references the SinterPrints logo aesthetic.

#### Acceptance Criteria

1. THE Login_Card SHALL be centered both vertically and horizontally within the viewport for viewport widths from 320px to 3840px
2. THE Login_Card SHALL have a dark background color (#1A1A1A or #222222) with a border radius between 8px and 16px and a box shadow with blur radius between 8px and 20px
3. THE Login_Card SHALL use white (#FFFFFF) as the primary text color for labels and placeholders
4. THE Login_Card SHALL have a maximum width between 400px and 480px, and on viewports narrower than 576px SHALL occupy at least 90% of the viewport width while maintaining centered positioning
5. THE Login_Card SHALL have internal padding between 24px and 48px so that child elements do not touch the card edges

### Requirement 3: Application Logo Display

**User Story:** As a user, I want to see the SinterPrints logo at the top of the login card, so that I can immediately identify the application.

#### Acceptance Criteria

1. THE Login_Card SHALL display the application logo image horizontally centered at the top of the card, above the email input field
2. THE Login_Page SHALL load the logo from the static file path "wwwroot/images/logo.png"
3. THE Login_Card SHALL render the logo with a maximum width of 80% of the card width and a maximum height of 120px, preserving the image aspect ratio
4. THE Login_Card SHALL render the logo image with an alt attribute containing the text "SinterPrints"
5. IF the logo image fails to load, THEN THE Login_Card SHALL display the alt text "SinterPrints" in place of the image so the application remains identifiable

### Requirement 4: Email Input Field

**User Story:** As a user, I want an email input field with clear visual styling, so that I can enter my login credentials.

#### Acceptance Criteria

1. THE Login_Form SHALL contain an email input field of type="email" positioned below the logo, with a placeholder text "Email" and an associated accessible label
2. THE Login_Form SHALL apply a 1px solid cyan border (#00BFFF) to the email input field in its default state
3. THE Login_Form SHALL enforce a maximum length of 254 characters on the email input field
4. WHEN the email field is left empty and the form is submitted, THE Login_Form SHALL display a validation error message indicating the email is required
5. WHEN the email field contains an invalid email format and the form is submitted, THE Login_Form SHALL display a validation error message indicating the format is invalid

### Requirement 5: Password Input Field

**User Story:** As a user, I want a password input field that masks my input, so that my credentials remain private.

#### Acceptance Criteria

1. THE Login_Form SHALL contain a password input field (type="password") below the email field that masks all entered characters with a bullet or dot symbol
2. THE Login_Form SHALL apply a cyan border (#00BFFF) to the password input field
3. WHEN the password field is left empty and the form is submitted, THE Login_Form SHALL display a validation error message indicating the password is required
4. WHEN the password field contains fewer than 6 characters and the form is submitted, THE Login_Form SHALL display a validation error message indicating the minimum length requirement

### Requirement 6: Entrar Button Styling

**User Story:** As a user, I want the submit button to be visually prominent with brand accent colors, so that the primary action is immediately obvious.

#### Acceptance Criteria

1. THE Entrar_Button SHALL display the text "Entrar"
2. THE Entrar_Button SHALL use a magenta-based background (#FF0066 or #E91E8C) or a magenta-to-yellow gradient as its default style
3. WHEN the user hovers over the Entrar_Button, THE Entrar_Button SHALL transition to a yellow/gold accent color (#FFCC00 or #FFC107) with a CSS transition duration between 200ms and 400ms
4. THE Entrar_Button SHALL use white (#FFFFFF) text on all background states and maintain a minimum contrast ratio of 4.5:1 between the text color and the background color
5. THE Entrar_Button SHALL span the full width of the Login_Card content area

### Requirement 7: Button Loading State

**User Story:** As a developer, I want the Entrar button to support a loading/disabled state, so that future authentication integration can indicate processing to the user.

#### Acceptance Criteria

1. WHILE the Login_Form is in a submitting state, THE Entrar_Button SHALL have the HTML `disabled` attribute set, reducing its opacity to 0.6 or lower and changing the cursor to `not-allowed`, preventing any click events from triggering additional submissions
2. WHILE the Login_Form is in a submitting state, THE Entrar_Button SHALL replace its default "Entrar" text with a visible spinner element alongside the text "Entrando..." to indicate processing
3. WHEN the submitting state ends, THE Entrar_Button SHALL remove the `disabled` attribute, restore full opacity, restore the default cursor, and display the original "Entrar" text with the styling defined in Requirement 6
4. IF the user clicks the Entrar_Button while the Login_Form is already in a submitting state, THEN THE Login_Form SHALL ignore the click and not initiate a duplicate submission

### Requirement 8: Form Validation with DataAnnotations

**User Story:** As a developer, I want the login form to use EditForm with DataAnnotations, so that validation follows Blazor conventions and is easily extensible.

#### Acceptance Criteria

1. THE Login_Form SHALL use Blazor's EditForm component bound to a Login_Model instance with an OnValidSubmit callback for form submission handling
2. THE Login_Model SHALL annotate the Email property with [Required], [EmailAddress], and [StringLength(256)] DataAnnotation attributes
3. THE Login_Model SHALL annotate the Password property with [Required] and [StringLength(128, MinimumLength = 1)] DataAnnotation attributes
4. WHEN the form is submitted with invalid data, THE Login_Form SHALL prevent submission and display a ValidationMessage component adjacent to each field that failed validation
5. WHEN the form is submitted with valid data, THE Login_Form SHALL invoke the OnValidSubmit callback without displaying any validation messages

### Requirement 9: Isolated CSS Styling

**User Story:** As a developer, I want styles scoped to the login page component via isolated CSS, so that login styles do not leak into other pages.

#### Acceptance Criteria

1. THE Login_Page SHALL define all gradient background, card positioning, card appearance, input field, and button styles in a co-located isolated CSS file named matching the component (e.g., Login.razor.css)
2. THE Login_Page SHALL not define its background gradient, card layout, card background color, or element spacing styles in any global stylesheet (such as app.css or site.css), permitting only framework resets and base font declarations in global CSS
3. WHEN a page other than Login_Page is rendered, THE application SHALL not apply any Login_Page-specific styles (gradient, dark card background, magenta button colors) to that page's elements

### Requirement 10: Clean and Modern Design

**User Story:** As a user, I want the login page to feel clean and modern without unnecessary visual clutter, so that the experience is focused and professional.

#### Acceptance Criteria

1. THE Login_Card SHALL contain only the following interactive and visual elements: the application logo, the email input field, the password input field, the Entrar button, and conditionally displayed validation error messages — no navigation links, registration links, decorative images, or additional text
2. THE Login_Card SHALL center-align all child elements horizontally and arrange them in a single vertical column with a uniform vertical gap of at least 16px between consecutive elements
3. THE Login_Card SHALL apply internal padding of at least 24px on all sides so that no child element touches the card border

### Requirement 11: Blazor Page Routing

**User Story:** As a developer, I want the login page accessible at a defined route, so that users can navigate directly to it.

#### Acceptance Criteria

1. THE Login_Page SHALL be routable at "/login" as the login entry point using the @page directive
2. THE Login_Page SHALL reside in the src/Web project following the project's mono-repo structure
3. THE Login_Page SHALL also be routable at "/" as the application's default landing page

### Requirement 12: Code-Behind Separation

**User Story:** As a developer, I want the login page logic separated into a code-behind file, so that the component follows project conventions for maintainability.

#### Acceptance Criteria

1. THE Login_Page SHALL use a code-behind file (.razor.cs) containing a partial class that defines the Login_Model class, form state fields, and the form submission event handler method
2. THE Login_Page .razor file SHALL contain only Razor markup and directives, with no @code block
3. THE Login_Page code-behind file SHALL compile without nullable reference type warnings when the project-level Nullable setting is set to "enable"
