package xchange.validator;

import com.amazonaws.services.lambda.runtime.Context;
import com.amazonaws.services.lambda.runtime.RequestHandler;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * The xchange-validator Lambda. Input {"rulesetId", "xml"}; output
 * {"rulesetId", "release", "failures": [{"flag", "ruleId", "message", "location"}]},
 * or {"error"} for a request it cannot handle.
 *
 * The rules are compiled in the static initialiser, which Lambda SnapStart
 * captures in its snapshot: a cold start restores compiled rules instead of
 * compiling them again.
 */
public final class Handler implements RequestHandler<Map<String, Object>, Map<String, Object>> {

    private static final OfficialRules RULES = OfficialRules.fromClasspath();

    @Override
    public Map<String, Object> handleRequest(Map<String, Object> input, Context context) {
        Object rulesetId = input == null ? null : input.get("rulesetId");
        Object xml = input == null ? null : input.get("xml");

        if (!(rulesetId instanceof String id) || !(xml instanceof String document)) {
            return Map.of("error", "Expected {\"rulesetId\": string, \"xml\": string}.");
        }
        if (!RULES.knows(id)) {
            return Map.of("error", "Unknown ruleset: " + id + ". Known: " + RULES.rulesetIds());
        }

        OfficialRules.Result result = RULES.validate(id, document);

        List<Map<String, Object>> failures = new ArrayList<>();
        for (OfficialRules.Failure failure : result.failures()) {
            Map<String, Object> entry = new LinkedHashMap<>();
            entry.put("flag", failure.flag());
            entry.put("ruleId", failure.ruleId());
            entry.put("message", failure.message());
            entry.put("location", failure.location());
            failures.add(entry);
        }

        Map<String, Object> response = new LinkedHashMap<>();
        response.put("rulesetId", result.rulesetId());
        response.put("release", result.release());
        response.put("failures", failures);
        return response;
    }
}
