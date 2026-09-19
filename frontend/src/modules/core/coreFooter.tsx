import type { PublicFooterContribution } from '../../platform/footerContributions';
import { useAporteDeFooter } from '../../platform/footerState';
import {
  hasPublicContact,
  publicContactFromSettings,
} from '../../platform/publicContact';
import { PublicContactDetails } from '../../platform/PublicContactDetails';
import { usePublicSettings } from '../../platform/usePublicSettings';

export const coreFooter: PublicFooterContribution = {
  moduleCode: 'core',
  Component: CoreFooterBlock,
};

function CoreFooterBlock() {
  const contact = publicContactFromSettings(usePublicSettings().all);
  const hasContent = hasPublicContact(contact);

  useAporteDeFooter(hasContent ? 'con-contenido' : 'vacio');

  if (!hasContent) {
    return null;
  }

  return (
    <section className="pf-footer__contact" aria-label="Información de contacto">
      <h2 className="pf-footer__heading">Contacto</h2>
      <PublicContactDetails contact={contact} whatsappLabel="WhatsApp" />
    </section>
  );
}
