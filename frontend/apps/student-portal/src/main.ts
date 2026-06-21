import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';
import { environment } from './environments/environment';

// Expose runtime config for the student data-access lib (avoids importing environment there).
// The registration wizard reads the tenant slug + terms version through the same window shim
// as the API URL, so a deployment can override them without a rebuild (contract §F).
const globalConfig = window as unknown as {
  __SB_API_URL__: string;
  __SB_TENANT__: string;
  __SB_TERMS_VERSION__: string;
};
globalConfig.__SB_API_URL__ = environment.apiUrl;
globalConfig.__SB_TENANT__ = environment.tenantSlug;
globalConfig.__SB_TERMS_VERSION__ = environment.termsVersion;

bootstrapApplication(AppComponent, appConfig).catch((err) => console.error(err));
