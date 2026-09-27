# Play review access — 왕국군 키우기

Prepared: 2026-09-24. Updated: 2026-09-27. Brand: Ludos Interactive. Scope: gameplay demo with monetization interface previews only.

**Internal draft. Do not submit until the review route and every placeholder below have been verified with the release build.**
The current project requires an authenticated session. The current Guest button connects to a shared development account; it is not a verified production review route.

## Console selection

Select the option indicating that all or some functionality is restricted.

## Instruction name

왕국군 키우기 — Sign-in and gameplay review

## Access credentials

Provide working credentials directly in the designated Play Console fields after verification. Do not store passwords, recovery codes, or access tokens in this repository.

## Instructions for the reviewer

1. Launch 왕국군 키우기.
2. On the title screen, open the sign-in panel.
3. Select [TO CONFIRM: the exact sign-in option available in the release build].
4. Sign in using the review credentials provided in the dedicated fields.
5. Complete [TO CONFIRM: any first-launch notices or required consent screens].
6. Enter the game and confirm that the main battle screen is displayed.
7. Use the main menu to access character growth, equipment, dungeons, item draws, and Mage Tower skills. The interface is primarily in Korean.

Menu labels:
- 육성: Growth
- 왕국군: Army
- 장비: Equipment
- 던전: Dungeons
- 뽑기: Draws
- 마탑: Mage Tower
- 설정: Settings
- 상점: Shop
- 광고 보고 2배 받기: Watch an ad for double rewards (not connected in this demo)

Access to progression-gated features: [TO CONFIRM: the review account's verified progression and exact steps to reach each restricted feature].

Monetization interface in this demo: the Shop is available from the in-game hamburger menu and opens as a popup. Product cards show planned prices and contents, but do not initiate billing, display ads, deduct currency, or deliver the previewed products. Tapping these actions shows a Korean message explaining that the feature is not yet implemented. This behavior is the same for reviewers and ordinary players.

The offline-reward popup includes a preview button for double rewards through an ad. The Shop also previews separate daily ad rewards for equipment and Mage Tower tickets. These ad actions do not play a video or grant the advertised extra reward in this demo. Normal gameplay and the ordinary offline reward remain available without a purchase or ad. Confirm these statements against the exact submitted build before copying them into Console.

Network or location requirements: [TO CONFIRM: review access works from the review team's location and does not require contacting the developer for one-time codes].

## Internal completion checklist — do not paste into Console

- [ ] Exact release package and Play-delivered signing configuration verified.
- [ ] Account is separate from developer, admin, and ordinary player accounts.
- [ ] Sign-in is reusable; no expiring password or developer-mediated OTP is required.
- [ ] No location restriction prevents review access.
- [ ] All named menu labels match the release UI.
- [ ] Gated gameplay can be inspected; no purchase or ad is required for this demo. Preview controls behave exactly as described.
- [ ] A fresh installation successfully reaches gameplay using these instructions.
- [ ] Instructions do not conceal any functionality or describe a different app behavior for review.
- [ ] All `[TO CONFIRM: ...]` text removed after actual verification.

[Google's sign-in information requirements](https://support.google.com/googleplay/android-developer/answer/15748846)

## Release blockers recorded on 2026-09-27

The device sign-in screen currently says that login implies agreement to the terms and privacy policy. Working policy links, a verified consent flow, and the under-14 guardian process have not been demonstrated. Do not describe those as complete in reviewer instructions. The exact sign-in route, dedicated reusable review access, guardian flow, and account deletion still require release-build verification. The website cannot substitute for these app capabilities.
