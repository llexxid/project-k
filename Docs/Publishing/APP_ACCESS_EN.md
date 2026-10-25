# Play review access — 왕국군 키우기

Prepared: 2026-09-24.

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

Access to progression-gated features: [TO CONFIRM: the review account's verified progression and exact steps to reach each restricted feature].

Access to paid features: [TO CONFIRM: how reviewers can examine restricted features without making a personal payment; describe only the verified release behavior].

Network or location requirements: [TO CONFIRM: review access works from the review team's location and does not require contacting the developer for one-time codes].

## Internal completion checklist — do not paste into Console

- [ ] Exact release package and Play-delivered signing configuration verified.
- [ ] Account is separate from developer, admin, and ordinary player accounts.
- [ ] Sign-in is reusable; no expiring password or developer-mediated OTP is required.
- [ ] No location restriction prevents review access.
- [ ] All named menu labels match the release UI.
- [ ] Gated and paid functionality can be inspected using the documented route.
- [ ] A fresh installation successfully reaches gameplay using these instructions.
- [ ] Instructions do not conceal any functionality or describe a different app behavior for review.
- [ ] All `[TO CONFIRM: ...]` text removed after actual verification.

[Google's sign-in information requirements](https://support.google.com/googleplay/android-developer/answer/15748846)
