package xchange.validator;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.StringReader;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import javax.xml.transform.stream.StreamSource;
import net.sf.saxon.s9api.Processor;
import net.sf.saxon.s9api.QName;
import net.sf.saxon.s9api.SaxonApiException;
import net.sf.saxon.s9api.XPathCompiler;
import net.sf.saxon.s9api.XPathExecutable;
import net.sf.saxon.s9api.XPathSelector;
import net.sf.saxon.s9api.XdmDestination;
import net.sf.saxon.s9api.XdmItem;
import net.sf.saxon.s9api.XdmNode;
import net.sf.saxon.s9api.XsltExecutable;

/**
 * The official rules of every ruleset in rulesets/index.txt, compiled once and
 * run on demand. The index and the compiled XSLT files come from
 * validation/rulesets.json via validation/fetch.py, so this class runs exactly
 * the artefacts that manifest pins - in the Lambda and in CI alike.
 *
 * Compiling takes seconds; validating takes milliseconds. Instances are meant
 * to be created once and shared: XsltExecutable is thread-safe.
 */
public final class OfficialRules {

    /** One failed assertion from the official rules. */
    public record Failure(String flag, String ruleId, String message, String location) {
        public boolean isFatal() {
            return "fatal".equals(flag);
        }
    }

    public record Result(String rulesetId, String release, List<Failure> failures) {
        public boolean hasFatal() {
            return failures.stream().anyMatch(Failure::isFatal);
        }
    }

    private record Rules(String name, XsltExecutable stylesheet) {}

    private record Ruleset(String id, String release, List<Rules> rules) {}

    private static final String SVRL = "http://purl.oclc.org/dsdl/svrl";

    private final Processor processor = new Processor(false);
    private final Map<String, Ruleset> rulesets = new LinkedHashMap<>();
    private final XPathExecutable failedAsserts;
    private final XPathExecutable assertText;

    private OfficialRules() throws SaxonApiException {
        XPathCompiler xpath = processor.newXPathCompiler();
        xpath.declareNamespace("svrl", SVRL);
        failedAsserts = xpath.compile("//svrl:failed-assert");
        assertText = xpath.compile("normalize-space(svrl:text)");
    }

    /** Loads and compiles every ruleset listed in rulesets/index.txt on the classpath. */
    public static OfficialRules fromClasspath() {
        try {
            OfficialRules rules = new OfficialRules();
            ClassLoader loader = OfficialRules.class.getClassLoader();
            try (InputStream index = loader.getResourceAsStream("rulesets/index.txt")) {
                if (index == null) {
                    throw new IllegalStateException("rulesets/index.txt is not on the classpath; run validator/build.sh");
                }
                BufferedReader reader = new BufferedReader(new InputStreamReader(index, StandardCharsets.UTF_8));
                for (String line; (line = reader.readLine()) != null; ) {
                    if (!line.isBlank()) {
                        rules.load(line, loader);
                    }
                }
            }
            return rules;
        } catch (IOException | SaxonApiException e) {
            throw new IllegalStateException("Could not load the official rules", e);
        }
    }

    /** One index line: id, release, then name=file for each artefact, tab-separated. */
    private void load(String line, ClassLoader loader) throws SaxonApiException {
        String[] fields = line.split("\t");
        String id = fields[0];
        List<Rules> compiled = new ArrayList<>();
        for (int i = 2; i < fields.length; i++) {
            String[] nameAndFile = fields[i].split("=", 2);
            URL stylesheet = loader.getResource("rulesets/" + id + "/" + nameAndFile[1]);
            if (stylesheet == null) {
                throw new IllegalStateException("Missing rules file for " + id + ": " + nameAndFile[1]);
            }
            compiled.add(new Rules(nameAndFile[0], processor.newXsltCompiler().compile(new StreamSource(stylesheet.toExternalForm()))));
        }
        rulesets.put(id, new Ruleset(id, fields[1], List.copyOf(compiled)));
    }

    public boolean knows(String rulesetId) {
        return rulesets.containsKey(rulesetId);
    }

    public List<String> rulesetIds() {
        return List.copyOf(rulesets.keySet());
    }

    /**
     * Runs every artefact of the ruleset over the document and collects every
     * failed assertion. A document that is not well-formed XML is one fatal
     * failure, not an exception.
     */
    public Result validate(String rulesetId, String xml) {
        Ruleset ruleset = rulesets.get(rulesetId);
        if (ruleset == null) {
            throw new IllegalArgumentException("Unknown ruleset: " + rulesetId);
        }

        List<Failure> failures = new ArrayList<>();
        for (Rules rules : ruleset.rules()) {
            try {
                XdmDestination report = new XdmDestination();
                rules.stylesheet().load30().transform(new StreamSource(new StringReader(xml)), report);
                collect(report.getXdmNode(), failures);
            } catch (SaxonApiException e) {
                failures.add(new Failure("fatal", "XML", "The document could not be read as XML: " + e.getMessage(), "/"));
                break;
            }
        }
        return new Result(ruleset.id(), ruleset.release(), List.copyOf(failures));
    }

    private void collect(XdmNode report, List<Failure> failures) throws SaxonApiException {
        XPathSelector select = failedAsserts.load();
        select.setContextItem(report);
        for (XdmItem item : select) {
            XdmNode failed = (XdmNode) item;
            XPathSelector text = assertText.load();
            text.setContextItem(failed);
            String flag = failed.getAttributeValue(new QName("flag"));
            failures.add(new Failure(
                    flag == null ? "fatal" : flag,
                    failed.getAttributeValue(new QName("id")),
                    withoutRuleIdPrefix(text.evaluateSingle().getStringValue()),
                    readableLocation(failed.getAttributeValue(new QName("location")))));
        }
    }

    /** "[BR-CO-16]-Amount due..." -> "Amount due...": the rule id travels separately. */
    static String withoutRuleIdPrefix(String message) {
        return message.replaceFirst("^\\[[^\\]]+\\]-", "");
    }

    /**
     * "/*:Invoice[namespace-uri()='urn:...'][1]/*:LegalMonetaryTotal[namespace-uri()='urn:...'][1]"
     * -> "/Invoice[1]/LegalMonetaryTotal[1]". The namespaces add nothing for a reader.
     */
    static String readableLocation(String location) {
        return location == null ? "" : location.replaceAll("\\*:([A-Za-z0-9]+)\\[namespace-uri\\(\\)='[^']*'\\]", "$1");
    }
}
