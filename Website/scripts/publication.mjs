import {readFile} from 'node:fs/promises';
import path from 'node:path';

// Operator review gates are evidence requirements, not a legal approval service.
export async function validatePublication(root, config) {
  const requiredChecks = ['businessIdentityVerified','privacyDataVerified','childConsentVerified',
    'deletionProcessVerified','supportMailboxVerified','policyApproved'];
  const pending = requiredChecks.filter(name => config.publicationChecks?.[name] !== true);
  for (const name of ['legalEntity', 'representative', 'businessAddress', 'businessNumber', 'businessPhone', 'effectiveDate']) {
    if (!config[name]?.trim()) pending.push(name);
  }
  try {
    const origin = new URL(config.publicOrigin);
    if (origin.protocol !== 'https:' || origin.pathname !== '/' || origin.search || origin.hash || origin.username || origin.password)
      pending.push('publicOrigin must be an HTTPS origin');
  } catch { pending.push('publicOrigin'); }
  if (!/^\d{4}-\d{2}-\d{2}$/.test(config.effectiveDate || '')) pending.push('effectiveDate YYYY-MM-DD');
  if (!Number.isFinite(Date.parse(config.effectiveDate))) pending.push('valid effectiveDate');
  if (config.mailOrderStatus === 'registered') {
    if (!config.mailOrderRegistration?.trim() || !config.mailOrderAuthority?.trim()) pending.push('mail order registration and authority');
  } else if (!['exempt','notApplicableForCurrentDemo'].includes(config.mailOrderStatus) || !config.mailOrderBasis?.trim()) {
    pending.push('verified mail order registration status or exemption basis');
  }
  const probabilities = JSON.parse(await readFile(path.join(root, '../Docs/Publishing/data/probabilities.json'), 'utf8'));
  if (!probabilities.publicationReady || !probabilities.effectiveAt) pending.push('verified probability disclosure');
  for (const file of ['PRIVACY_POLICY_KO.md', 'TERMS_OF_SERVICE_KO.md', 'ACCOUNT_DELETION_KO.md', 'PROBABILITY_DISCLOSURE.md']) {
    const source = await readFile(path.join(root, '../Docs/Publishing', file), 'utf8');
    const content = source.split('<!-- PUBLIC-CONTENT:START -->')[1]?.split('<!-- PUBLIC-CONTENT:END -->')[0];
    if (!content?.trim()) pending.push(file + ' public content');
    const readiness = [...source.matchAll(/`publicationReady:\s*(true|false)`/g)];
    if (readiness.length !== 1 || readiness[0][1] !== 'true') pending.push(file + ' publicationReady');
  }
  if (pending.length) throw new Error('Publication not ready: ' + pending.join(', '));
}
