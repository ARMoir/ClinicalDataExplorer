from http.server import BaseHTTPRequestHandler, HTTPServer
from urllib.parse import urlparse, parse_qs

patient="<Patient><id value='p1'/><active value='true'/><identifier><value value='TEST-001'/></identifier><name><family value='Example'/><given value='Alex'/></name><gender value='unknown'/><birthDate value='1980-01-01'/><generalPractitioner><reference value='Practitioner/pr1'/></generalPractitioner></Patient>"
encounter="<Encounter><id value='e1'/><status value='finished'/><class><code value='AMB'/></class><subject><reference value='Patient/p1'/></subject><period><start value='2026-09-01T12:00:00Z'/></period><participant><individual><reference value='Practitioner/pr1'/></individual></participant></Encounter>"
doctor="<Practitioner><id value='pr1'/><name><text value='Dr Example'/></name></Practitioner>"
observation="<Observation><id value='o1'/><status value='final'/><code><text value='Example lab'/></code><subject><reference value='Patient/p1'/></subject><encounter><reference value='Encounter/e1'/></encounter><effectiveDateTime value='2026-09-01T12:00:00Z'/><valueQuantity><value value='12'/><unit value='mg/L'/></valueQuantity><interpretation><coding><code value='H'/><display value='High'/></coding></interpretation><referenceRange><low><value value='3'/></low><high><value value='10'/></high></referenceRange><performer><reference value='Practitioner/pr1'/></performer></Observation>"
report="<DiagnosticReport><id value='r1'/><identifier><value value='REPORT-001'/></identifier><status value='final'/><code><text value='Example report'/></code><subject><reference value='Patient/p1'/></subject><encounter><reference value='Encounter/e1'/></encounter><effectiveDateTime value='2026-09-01T12:00:00Z'/><result><reference value='Observation/o1'/></result><performer><reference value='Practitioner/pr1'/></performer></DiagnosticReport>"
def bundle(*resources): return "<Bundle xmlns='http://hl7.org/fhir'><type value='searchset'/><total value='1'/>"+''.join('<entry><resource>'+r+'</resource></entry>' for r in resources)+'</Bundle>'
def standalone(resource): return resource.replace('>', " xmlns='http://hl7.org/fhir'>",1)
class Handler(BaseHTTPRequestHandler):
 def do_GET(self):
  uri=urlparse(self.path); path=uri.path; query=parse_qs(uri.query)
  if query.get('_summary')==['count']: result=bundle()
  elif path.endswith('metadata'): result="<CapabilityStatement xmlns='http://hl7.org/fhir'><status value='active'/><fhirVersion value='4.0.1'/><format value='xml'/><rest><mode value='server'/><resource><type value='Organization'/><interaction><code value='search-type'/></interaction></resource></rest></CapabilityStatement>"
  elif path.endswith('$everything'): result=bundle(patient,encounter,observation,report,doctor)
  elif path.endswith('Patient/p1'): result=standalone(patient)
  elif path.endswith('Encounter/e1'): result=standalone(encounter)
  elif path.endswith('Practitioner/pr1'): result=standalone(doctor)
  elif path.endswith('Observation/o1'): result=standalone(observation)
  elif path.endswith('Patient'): result=bundle(patient)
  elif path.endswith('Encounter'): result=bundle(encounter,patient,doctor)
  elif path.endswith('Observation'): result=bundle(observation)
  elif path.endswith('DiagnosticReport'): result=bundle(report,observation,doctor)
  elif path.endswith('Practitioner'): result=bundle(doctor)
  else: result=bundle()
  self.send_response(200); self.send_header('Content-Type','application/fhir+xml'); self.end_headers(); self.wfile.write(result.encode())
 def log_message(self,*args): pass
HTTPServer(('127.0.0.1',5198),Handler).serve_forever()
